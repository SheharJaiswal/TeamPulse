import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DEFINITIONS_PATH = ROOT / "infra" / "rabbitmq" / "definitions.json"
EXCHANGE = "teampulse.domain.events"
ROUTING_KEY = "identity.employee.lifecycle"
HABIT_QUEUE = "teampulse.habit.events"
HABIT_DLQ = "teampulse.habit.events.dlq"
EVENT_TRACKING_QUEUE = "teampulse.event-tracking.events"
EVENT_TRACKING_DLQ = "teampulse.event-tracking.events.dlq"
EVENT_TRACKING_DEAD_ROUTING_KEY = "event-tracking.employee.lifecycle.dead"
NOTIFICATION_QUEUE = "teampulse.notification.events"
NOTIFICATION_DLQ = "teampulse.notification.events.dlq"
NOTIFICATION_DEAD_ROUTING_KEY = "notification.employee.lifecycle.dead"
INSIGHT_QUEUE = "teampulse.insight.events"
ACTIVITY_QUEUE = "teampulse.activity.events"
ACTIVITY_DLQ = "teampulse.activity.events.dlq"
ACTIVITY_DEAD_ROUTING_KEY = "habit.activity.recorded.dead"
DLX = "teampulse.domain.events.dlx"
DEAD_ROUTING_KEY = "habit.employee.lifecycle.dead"
SERVICE_QUEUES = {
    HABIT_QUEUE, "teampulse.challenge.events", EVENT_TRACKING_QUEUE,
    INSIGHT_QUEUE, NOTIFICATION_QUEUE,
}


class RabbitMqDefinitionsContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.definitions = json.loads(DEFINITIONS_PATH.read_text(encoding="utf-8"))

    def test_domain_exchange_is_a_durable_topic_exchange(self):
        exchanges = {item["name"]: item for item in self.definitions["exchanges"]}
        self.assertEqual("topic", exchanges[EXCHANGE]["type"])
        self.assertTrue(exchanges[EXCHANGE]["durable"])

    def test_habit_dead_letter_exchange_is_durable_and_direct(self):
        exchanges = {item["name"]: item for item in self.definitions["exchanges"]}
        self.assertEqual("direct", exchanges[DLX]["type"])
        self.assertTrue(exchanges[DLX]["durable"])

    def test_service_queues_and_dlqs_are_defined(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        expected = SERVICE_QUEUES | {HABIT_DLQ, "teampulse.challenge.events.dlq",
            EVENT_TRACKING_DLQ, NOTIFICATION_DLQ, ACTIVITY_QUEUE, ACTIVITY_DLQ}
        self.assertEqual(expected, set(queues))
        self.assertTrue(all(queues[name]["durable"] for name in queues))

    def test_habit_source_queue_is_quorum_with_bounded_at_least_once_dead_lettering(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        arguments = queues[HABIT_QUEUE]["arguments"]
        self.assertEqual("quorum", arguments["x-queue-type"])
        self.assertEqual(5, arguments["x-delivery-limit"])
        self.assertEqual(DLX, arguments["x-dead-letter-exchange"])
        self.assertEqual(DEAD_ROUTING_KEY, arguments["x-dead-letter-routing-key"])
        self.assertEqual("at-least-once", arguments["x-dead-letter-strategy"])
        self.assertEqual("reject-publish", arguments["x-overflow"])

    def test_habit_dlq_is_a_durable_quorum_queue(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertEqual("quorum", queues[HABIT_DLQ]["arguments"]["x-queue-type"])

    def test_challenge_source_queue_is_quorum_with_bounded_at_least_once_dead_lettering(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        arguments = queues["teampulse.challenge.events"]["arguments"]
        self.assertEqual("quorum", arguments["x-queue-type"])
        self.assertEqual(5, arguments["x-delivery-limit"])
        self.assertEqual(DLX, arguments["x-dead-letter-exchange"])
        self.assertEqual("challenge.employee.lifecycle.dead", arguments["x-dead-letter-routing-key"])
        self.assertEqual("at-least-once", arguments["x-dead-letter-strategy"])
        self.assertEqual("reject-publish", arguments["x-overflow"])

    def test_challenge_dead_letter_queue_is_a_durable_quorum_queue(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertEqual("quorum", queues["teampulse.challenge.events.dlq"]["arguments"]["x-queue-type"])

    def test_challenge_dead_letter_queue_has_its_own_binding(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((DLX, "teampulse.challenge.events.dlq", "challenge.employee.lifecycle.dead"), bindings)

    def test_event_tracking_source_queue_is_quorum_with_bounded_at_least_once_dead_lettering(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        arguments = queues[EVENT_TRACKING_QUEUE]["arguments"]
        self.assertEqual("quorum", arguments["x-queue-type"])
        self.assertEqual(5, arguments["x-delivery-limit"])
        self.assertEqual(DLX, arguments["x-dead-letter-exchange"])
        self.assertEqual(EVENT_TRACKING_DEAD_ROUTING_KEY, arguments["x-dead-letter-routing-key"])
        self.assertEqual("at-least-once", arguments["x-dead-letter-strategy"])
        self.assertEqual("reject-publish", arguments["x-overflow"])

    def test_event_tracking_dead_letter_queue_is_a_durable_quorum_queue(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertEqual("quorum", queues[EVENT_TRACKING_DLQ]["arguments"]["x-queue-type"])

    def test_event_tracking_dead_letter_queue_has_its_own_binding(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((DLX, EVENT_TRACKING_DLQ, EVENT_TRACKING_DEAD_ROUTING_KEY), bindings)

    def test_notification_source_queue_is_quorum_with_bounded_at_least_once_dead_lettering(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        arguments = queues[NOTIFICATION_QUEUE]["arguments"]
        self.assertEqual("quorum", arguments["x-queue-type"])
        self.assertEqual(5, arguments["x-delivery-limit"])
        self.assertEqual(DLX, arguments["x-dead-letter-exchange"])
        self.assertEqual(NOTIFICATION_DEAD_ROUTING_KEY, arguments["x-dead-letter-routing-key"])
        self.assertEqual("at-least-once", arguments["x-dead-letter-strategy"])
        self.assertEqual("reject-publish", arguments["x-overflow"])

    def test_notification_dead_letter_queue_is_a_durable_quorum_queue(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertEqual("quorum", queues[NOTIFICATION_DLQ]["arguments"]["x-queue-type"])

    def test_notification_dead_letter_queue_has_its_own_binding(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((DLX, NOTIFICATION_DLQ, NOTIFICATION_DEAD_ROUTING_KEY), bindings)

    def test_all_five_service_queues_keep_the_lifecycle_binding(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        expected = {(EXCHANGE, queue, ROUTING_KEY) for queue in SERVICE_QUEUES}
        self.assertTrue(expected.issubset(bindings))

    def test_habit_dead_letter_queue_has_its_own_binding(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((DLX, HABIT_DLQ, DEAD_ROUTING_KEY), bindings)

    def test_definitions_do_not_contain_users_or_credentials(self):
        self.assertNotIn("users", self.definitions)
        self.assertNotIn("permissions", self.definitions)

    def test_insight_existing_source_queue_remains_classic_until_safe_migration(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertTrue(queues[INSIGHT_QUEUE]["durable"])
        self.assertEqual([], queues[INSIGHT_QUEUE]["arguments"])
        self.assertNotIn("teampulse.insight.events.dlq", queues)

    def test_insight_source_lifecycle_binding_is_preserved_until_migration(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((EXCHANGE, INSIGHT_QUEUE, ROUTING_KEY), bindings)
        self.assertNotIn((DLX, INSIGHT_QUEUE, "insight.employee.lifecycle.dead"), bindings)

    def test_activity_event_queue_is_bounded_quorum_with_own_dead_letter_route(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        arguments = queues[ACTIVITY_QUEUE]["arguments"]
        self.assertEqual("quorum", arguments["x-queue-type"])
        self.assertEqual(5, arguments["x-delivery-limit"])
        self.assertEqual(100000, arguments["x-max-length"])
        self.assertEqual(DLX, arguments["x-dead-letter-exchange"])
        self.assertEqual(ACTIVITY_DEAD_ROUTING_KEY, arguments["x-dead-letter-routing-key"])
        self.assertEqual("at-least-once", arguments["x-dead-letter-strategy"])
        self.assertEqual("reject-publish", arguments["x-overflow"])
        self.assertEqual("quorum", queues[ACTIVITY_DLQ]["arguments"]["x-queue-type"])

    def test_activity_event_has_dedicated_source_and_dead_letter_bindings(self):
        bindings = {(item["source"], item["destination"], item["routing_key"]) for item in self.definitions["bindings"]}
        self.assertIn((EXCHANGE, ACTIVITY_QUEUE, "habit.activity.recorded"), bindings)
        self.assertIn((EXCHANGE, ACTIVITY_QUEUE, "challenge.activity.recorded"), bindings)
        self.assertIn((DLX, ACTIVITY_DLQ, ACTIVITY_DEAD_ROUTING_KEY), bindings)


if __name__ == "__main__":
    unittest.main()
