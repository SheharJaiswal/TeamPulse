import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DEFINITIONS_PATH = ROOT / "infra" / "rabbitmq" / "definitions.json"
EXCHANGE = "teampulse.domain.events"
ROUTING_KEY = "identity.employee.lifecycle"
EXPECTED_QUEUES = {
    "teampulse.habit.events",
    "teampulse.challenge.events",
    "teampulse.event-tracking.events",
    "teampulse.insight.events",
    "teampulse.notification.events",
}


class RabbitMqDefinitionsContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.definitions = json.loads(DEFINITIONS_PATH.read_text(encoding="utf-8"))

    def test_exactly_one_durable_topic_exchange_is_defined(self):
        exchanges = self.definitions["exchanges"]
        self.assertEqual(1, len(exchanges))
        self.assertEqual(EXCHANGE, exchanges[0]["name"])
        self.assertEqual("topic", exchanges[0]["type"])
        self.assertTrue(exchanges[0]["durable"])

    def test_exactly_five_durable_service_queues_are_defined(self):
        queues = self.definitions["queues"]
        self.assertEqual(EXPECTED_QUEUES, {queue["name"] for queue in queues})
        self.assertTrue(all(queue["durable"] for queue in queues))

    def test_each_service_queue_has_the_lifecycle_binding(self):
        bindings = self.definitions["bindings"]
        actual = {
            (binding["source"], binding["destination"], binding["routing_key"])
            for binding in bindings
        }
        expected = {(EXCHANGE, queue, ROUTING_KEY) for queue in EXPECTED_QUEUES}
        self.assertEqual(expected, actual)

    def test_definitions_do_not_contain_users_or_credentials(self):
        self.assertNotIn("users", self.definitions)
        self.assertNotIn("permissions", self.definitions)


if __name__ == "__main__":
    unittest.main()
