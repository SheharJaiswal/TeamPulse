# Design: Persistence guidance alignment

This is a documentation-only consistency correction. ADR-0001 remains the accepted decision and source of truth: transactional service databases use MySQL 8.4 through EF Core 8 and Pomelo. DynamoDB remains the analytics read-model store.

No source code, package references, schema, migrations or runtime configuration will change. The status checkpoint will identify the Identity service verification work as the next implementation priority. Build and runtime validation cannot be inferred from documentation consistency.
