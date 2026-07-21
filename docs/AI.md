# MissionPlanner AI Contributor Guide

## Purpose

This document defines how AI assistants should contribute to the MissionPlanner project.

MissionPlanner is **not** a port of the original Mission Planner.

It is a modern, cross-platform Ground Control Station built around Domain Driven Design,
clean architecture, immutable domain state and transport-independent MAVLink communication.

The architecture is considered part of the product.

When implementation details appear to conflict with the architecture, preserve the
architecture and discuss the implementation before changing architectural boundaries.

---

# Read First

Before implementing architectural changes, read:

1. docs/DESIGN_CONCEPTS.md
2. docs/ARCHITECTURE_DECISION_RECORDS.md
3. docs/FEATURES.md

Subsystem-specific work:

Vehicle communication
    docs/VEHICLE_CONNECTION.md

Parameters
    docs/PARAMETERS.md

Logging
    docs/SERILOG_QUICKSTART.md

Mission planning
    docs/MISSIONS.md

Do not duplicate architecture already described in these documents.

---

# General Working Style

Prefer understanding before implementation.

Explain assumptions.

Ask questions when architectural intent is unclear.

Prefer extending existing architecture over replacing it.

Large architectural rewrites require discussion before implementation.

When introducing a new subsystem:

- integrate with the existing architecture
- avoid creating parallel implementations
- preserve compatibility where practical

---

# Architecture

Follow the architecture described in DESIGN_CONCEPTS.md.

The fundamental layers are

UI

↓

Application

↓

Domain

↓

MAVLink

↓

Transport

These layers should remain independent.

Do not bypass layers.

---

# Communication Pipeline

Incoming telemetry always follows this pipeline.

Transport

↓

Parser

↓

Decoder

↓

VehicleMessagePump

↓

Capability Handler

↓

VehicleSession

↓

VehicleRegistry

↓

Domain Events

↓

Application Services

↓

ViewModels

Do not skip stages.

Do not update VehicleState directly.

---

# Domain Rules

VehicleState is immutable.

VehicleSession owns all state mutations.

MAVLink packets never leave the MAVLink project.

Domain code should never depend on MAVLink packet structures.

Convert MAVLink messages into domain observations.

Capability handlers bridge protocol and domain.

---

# Mission Rules

Mission planning belongs in the Core project.

Mission items are strongly typed.

Do not expose Param1–Param4 to the UI.

Validation occurs before upload.

Protocol mapping belongs in the MAVLink layer.

Mission transfer belongs in Application services.

---

# Performance

Communication code must minimize allocations.

Avoid expensive logging inside parsing loops.

Avoid LINQ inside hot paths.

Channels are only used inside the communication pipeline.

The EventHub remains the application's event system.

Measure performance before optimizing.

---

# Dependency Injection

Resolve services through dependency injection.

Avoid creating services with new.

Avoid service locators outside approved UI construction patterns.

Prefer constructor injection.

---

# Code Comments
Remember to add xml comments to all public types/members etc. 
Use build and check for CS1591/CS1587 warnings.

---
# Testing

Whenever practical:

write tests for

- protocol parsing
- decoder logic
- domain logic
- application services

UI tests are less important than protocol and domain tests.

---

# Refactoring

Prefer incremental migration.

Introduce compatibility layers where practical.

Avoid unnecessary API breaks.

Architecture improvements are preferred over cosmetic changes.

---

# Documentation

When introducing new functionality:

Update

- FEATURES.md

Update architecture docs if architectural concepts change.

Do not leave documentation inconsistent with implementation.

---

# Coding Style

Follow

.editorconfig

Use English for:

- comments
- XML documentation
- Markdown documentation

Prefer self-documenting code.

Comments should explain intent, assumptions or tradeoffs.

---

# AI Behaviour

Do not silently invent architecture.

Do not silently introduce frameworks.

Do not silently change layering.

Do not over-engineer.

Do not add abstractions for hypothetical future needs.

When uncertain:

Explain assumptions.

Ask.

Then implement.

---

# What Not To Do

Do not expose MAVLink packets to the UI.

Do not mutate VehicleState directly.

Do not bypass VehicleSession.

Do not use Channels outside communication pipelines.

Do not duplicate protocol knowledge.

Do not introduce hidden side effects.

Do not optimize before measuring.

---

# Project Goal

The goal is not merely to recreate Mission Planner.

The goal is to build a reusable Ground Control Station framework supporting

- multiple vehicles
- multiple transports
- mission planning
- autonomous flight
- simulation
- plugins
- AI-assisted workflows

while preserving a clean and maintainable architecture.
---

# Codex

Repository-level Codex instructions are stored in `../AGENTS.md`.

`AGENTS.md` is intentionally concise and points back to this guide, the documentation set,
and `.editorconfig`. Keep architecture and subsystem knowledge in the canonical documents
rather than duplicating it in agent-specific files.
