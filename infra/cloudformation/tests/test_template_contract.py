from __future__ import annotations

import json
import unittest
from collections import Counter
from pathlib import Path

import yaml
from yaml.nodes import MappingNode, ScalarNode, SequenceNode


ROOT = Path(__file__).resolve().parents[3]
TEMPLATE_PATH = ROOT / "infra" / "cloudformation" / "identity-eventbridge.yaml"


class CloudFormationLoader(yaml.SafeLoader):
    """Safe YAML loader that preserves CloudFormation intrinsic functions."""


def construct_intrinsic(loader: CloudFormationLoader, tag_suffix: str, node):
    if isinstance(node, ScalarNode):
        value = loader.construct_scalar(node)
    elif isinstance(node, SequenceNode):
        value = loader.construct_sequence(node)
    elif isinstance(node, MappingNode):
        value = loader.construct_mapping(node)
    else:
        raise TypeError(f"Unsupported CloudFormation YAML node: {type(node)!r}")

    if tag_suffix == "Ref":
        return {"Ref": value}
    return {f"Fn::{tag_suffix}": value}


CloudFormationLoader.add_multi_constructor("!", construct_intrinsic)


class IdentityEventBridgeTemplateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.template = yaml.load(TEMPLATE_PATH.read_text(encoding="utf-8"), Loader=CloudFormationLoader)
        cls.resources = cls.template["Resources"]
        cls.parameters = cls.template["Parameters"]

    def test_bus_role_and_queue_slo_are_explicit_parameters(self):
        for name in ("EnvironmentName", "EventBusName", "IdentityWorkloadRoleName", "TargetQueueAgeThresholdSeconds"):
            self.assertIn(name, self.parameters)
            self.assertNotIn("Default", self.parameters[name])

    def test_exactly_five_service_projection_queues_and_five_target_dlqs_exist(self):
        queues = [resource for resource in self.resources.values() if resource["Type"] == "AWS::SQS::Queue"]
        by_purpose = {}
        for resource in queues:
            tags = {tag["Key"]: tag["Value"] for tag in resource["Properties"]["Tags"]}
            by_purpose.setdefault(tags["Purpose"], []).append(resource)

        self.assertEqual(len(queues), 10)
        self.assertEqual(len(by_purpose["EmployeeStatusProjection"]), 5)
        self.assertEqual(len(by_purpose["EventBridgeTargetDeliveryDlq"]), 5)

        for resource in queues:
            self.assertTrue(resource["Properties"]["SqsManagedSseEnabled"])
            self.assertEqual(resource["DeletionPolicy"], "Retain")
            self.assertEqual(resource["UpdateReplacePolicy"], "Retain")
            self.assertNotIn("RedrivePolicy", resource["Properties"])

    def test_lifecycle_rule_matches_only_matching_outer_and_inner_event_types(self):
        rule = self.resources["EmployeeLifecycleProjectionRule"]
        pattern = rule["Properties"]["EventPattern"]
        self.assertEqual(pattern["source"], ["teampulse.identity"])
        self.assertEqual(
            pattern["$or"],
            [
                {
                    "detail-type": ["EmployeeDeactivated"],
                    "detail": {"eventType": ["EmployeeDeactivated"], "schemaVersion": [1]},
                },
                {
                    "detail-type": ["EmployeeReactivated"],
                    "detail": {"eventType": ["EmployeeReactivated"], "schemaVersion": [1]},
                },
            ],
        )
        targets = rule["Properties"]["Targets"]
        self.assertEqual(len(targets), 5)
        for target in targets:
            self.assertIn("DeadLetterConfig", target)
            self.assertEqual(
                target["RetryPolicy"]["MaximumEventAgeInSeconds"],
                {"Ref": "TargetRetryMaximumAgeSeconds"},
            )
            self.assertEqual(
                target["RetryPolicy"]["MaximumRetryAttempts"],
                {"Ref": "TargetRetryMaximumAttempts"},
            )
        self.assertEqual(len(rule["DependsOn"]), 5)

    def test_identity_put_events_permission_is_limited_to_the_custom_bus(self):
        policy = self.resources["IdentityPutEventsPolicy"]["Properties"]["PolicyDocument"]
        self.assertEqual(len(policy["Statement"]), 1)
        statement = policy["Statement"][0]
        self.assertEqual(statement["Action"], ["events:PutEvents"])
        self.assertEqual(statement["Effect"], "Allow")
        self.assertEqual(statement["Resource"], {"Fn::GetAtt": "IdentityLifecycleEventBus.Arn"})

    def test_each_queue_policy_is_limited_to_eventbridge_rule_and_account(self):
        policies = [resource for resource in self.resources.values() if resource["Type"] == "AWS::SQS::QueuePolicy"]
        self.assertEqual(len(policies), 5)
        for policy in policies:
            statements = policy["Properties"]["PolicyDocument"]["Statement"]
            self.assertEqual(len(statements), 2)
            for statement in statements:
                self.assertEqual(statement["Principal"], {"Service": "events.amazonaws.com"})
                self.assertEqual(statement["Action"], "sqs:SendMessage")
                self.assertEqual(
                    statement["Condition"]["ArnEquals"]["aws:SourceArn"],
                    {
                        "Fn::Sub": "arn:${AWS::Partition}:events:${AWS::Region}:${AWS::AccountId}:rule/${EventBusName}/${ResourceNamePrefix}-${EnvironmentName}-employee-lifecycle"
                    },
                )
                self.assertEqual(
                    statement["Condition"]["StringEquals"]["aws:SourceAccount"],
                    {"Ref": "AWS::AccountId"},
                )

    def test_alarms_cover_each_dlq_each_queue_and_eventbridge_dlq_write_failures(self):
        alarms = [resource for resource in self.resources.values() if resource["Type"] == "AWS::CloudWatch::Alarm"]
        self.assertEqual(len(alarms), 11)
        metric_counts = Counter(resource["Properties"]["MetricName"] for resource in alarms)
        self.assertEqual(metric_counts["ApproximateNumberOfMessagesVisible"], 5)
        self.assertEqual(metric_counts["ApproximateAgeOfOldestMessage"], 5)
        self.assertEqual(metric_counts["InvocationsFailedToBeSentToDlq"], 1)
        for alarm in alarms:
            self.assertEqual(
                alarm["Properties"]["AlarmActions"],
                {"Fn::If": ["HasAlarmNotificationTopic", [{"Ref": "AlarmNotificationTopicArn"}], {"Ref": "AWS::NoValue"}]},
            )

    def test_shared_stack_does_not_grant_consumer_read_permissions(self):
        serialized = json.dumps(self.resources)
        self.assertNotIn("sqs:ReceiveMessage", serialized)
        self.assertNotIn("sqs:DeleteMessage", serialized)
        self.assertNotIn("sqs:ChangeMessageVisibility", serialized)


if __name__ == "__main__":
    unittest.main()
