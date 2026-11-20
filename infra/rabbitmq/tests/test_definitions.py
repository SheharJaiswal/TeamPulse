import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DEFINITIONS_PATH = ROOT / "infra" / "rabbitmq" / "definitions.json"
EXCHANGE = "teampulse.domain.events"
ROUTING_KEY = "identity.employee.lifecycle"
HABIT_QUEUE = "teampulse.habit.events"
HABIT_DLQ = "teampulse.habit.events.dlq"
DLX = "teampulse.domain.events.dlx"
DEAD_ROUTING_KEY = "habit.employee.lifecycle.dead"
SERVICE_QUEUES = {
    HABIT_QUEUE,
    "teampulse.challenge.events",
    "teampulse.event-tracking.events",
    "teampulse.insight.events",
    "teampulse.notification.events",
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

    def test_five_service_queues_and_existing_service_dlqs_are_defined(self):
        queues = {item["name"]: item for item in self.definitions["queues"]}
        self.assertEqual(SERVICE_QUEUES | {HABIT_DLQ, "teampulse.challenge.events.dlq"}, set(queues))
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

    def test_all_five_service_queues_keep_the_lifecycle_binding(self):
        bindings = {
            (item["source"], item["destination"], item["routing_key"])
            for item in self.definitions["bindings"]
        }
        expected = {(EXCHANGE, queue, ROUTING_KEY) for queue in SERVICE_QUEUES}
        self.assertTrue(expected.issubset(bindings))

    def test_habit_dead_letter_queue_has_its_own_binding(self):
        bindings = {
            (item["source"], item["destination"], item["routing_key"])
            for item in self.definitions["bindings"]
        }
        self.assertIn((DLX, HABIT_DLQ, DEAD_ROUTING_KEY), bindings)

    def test_definitions_do_not_contain_users_or_credentials(self):
        self.assertNotIn("users", self.definitions)
        self.assertNotIn("permissions", self.definitions)


if __name__ == "__main__":
    unittest.main()
