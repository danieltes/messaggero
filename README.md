# Tools

PS> `powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"`

PS> `uv tool install specify-cli --from git+https://github.com/github/spec-kit.git`

# Initialization

PS> `specify init messaggero --ai copilot`

## Start working

- Open folder with VSCode.

- Open chat (I'm using Github Copilot + Claude Opus 4.6).

## First task / "Initial commit" (?)

- /speckit.constitution Create principles focused on code quality, testing standards, user experience consistency, and high performance and throughput requirements.

- /speckit.specify Build a library that can help me make use of different message buses like RabbitMQ and Kafka. I should be able to switch between different transports with minimal overhead.

- /speckit.clarify

- /speckit.plan The library should be installable as a nuget package. Use Confluent library for Kafka and RabbitMQ .NET Client library for RabbitMQ. It should completely be written in the C# latest version and .NET 10

- /speckit.tasks

- /speckit.implement

## Adding a new feature / Making changes to existing ones

- /speckit.specify Make it possible to use multiple transports simultaneously. I should be able to subscribe to RabbitMQ or to Kafka or to both at the same time. I should be able to publish a message and based on the destination, or the message type, the library should be able to decide which transport to use.

- /speckit.clarify

- /speckit.plan

- /speckit.implement

## Notes

- Since this is a library I should have used the term/expression "developer experience consistency" instead of "user experience consistency" in the _constitution_.

