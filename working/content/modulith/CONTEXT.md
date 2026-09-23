# ModulithTemplate

ModulithTemplate is a modular monolith: one deployable application divided into Features that own
their data and speak to each other only through published contracts.

This is the glossary — the words the code, the architecture tests and the documentation all have to
agree on. The terms below come from the architecture itself and are worth keeping as they are.
**Add your own domain terms to it as they settle**: when a word turns out to mean two things, or a
name in the code no longer matches how the team talks, resolve it here.

Each entry names the term, defines it, and lists the near-synonyms to _avoid_ so one concept keeps
one name.

## Architecture

**Feature**:
One vertical slice of the application, owning its own domain model, database schema and layer
projects. The unit of isolation: two Features share no types, tables or transactions.
_Avoid_: Module, service, component, bounded context

**Contract**:
The published surface a Feature offers to its siblings — a module API interface and the integration
events it emits. The only part of a Feature another Feature may reference.
_Avoid_: Public API, interface, facade

**Module API**:
A Feature's synchronous, read-only contract, answering "I need your data to finish the request I am
in right now".
_Avoid_: Service, gateway, client

## Events

**Domain Event**:
Something domain-significant that happened inside one Feature, named in that Feature's own language.
Internal: never referenced by another Feature, never leaves the transaction that raised it.
_Avoid_: Event, notification, internal event

**Integration Event**:
A Feature's announcement to its siblings that something happened, expressed in its published
language rather than its domain language. Part of the Contract, and durable — once it is recorded it
will be delivered.
_Avoid_: Message, external event, IEvent, broadcast

**Publisher**:
The Feature that owns an Integration Event and is the only one that may emit it. Ownership follows
the Contract: an Integration Event belongs to exactly one Feature.
_Avoid_: Producer, emitter, source

**Consumer**:
A Feature that reacts to another's Integration Event. An Integration Event may have any number,
including none — and none is a defect, not a state to rest in.
_Avoid_: Subscriber, listener, receiver, handler

**Group**:
The ordering boundary between Integration Events. Events in one Group are delivered one at a time in
the order they were recorded; different Groups proceed independently. Keyed by the aggregate the
event concerns.
_Avoid_: Partition, stream, queue, channel

## Failures

**Rejection**:
A request the application declined for an expected reason — a Validation Error, Not Found or a
Conflict. The system working as designed, and recorded as such rather than as a fault.
_Avoid_: Failure, error, exception

**Validation Error**:
A Rejection the user can put right by changing one of the values they supplied — a blank name, an
amount out of range, a name already taken. It belongs to the value that caused it.
_Avoid_: Invalid input, bad request, form error

**Not Found**:
A Rejection because the request names something that does not exist.
_Avoid_: Missing, 404, null result

**Conflict**:
A Rejection of a well-formed request that the current state forbids — the order has already shipped,
or someone else changed the record first. No change to the supplied values would fix it, which is
what separates it from a Validation Error.
_Avoid_: Business rule violation, domain error, concurrency error, 409

**Unexpected Error**:
A failure nobody anticipated — a bug or an infrastructure fault. Its details are recorded for the
team and never shown to the user, who receives only a reference to quote.
_Avoid_: Exception, crash, server error, internal error
