# ModulithTemplate

A modular monolith: one deployable application divided into Features that own their data and speak
to each other only through published contracts. This file is the glossary for that division — the
words the code, the tests and the architecture rules all have to agree on.

## Language

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

**Redelivery**:
Delivering an Integration Event again because a previous attempt did not complete. Expected rather
than exceptional: a Consumer must tolerate seeing the same event more than once, including after a
*sibling* Consumer was the one that failed.
_Avoid_: Retry, replay, duplicate
