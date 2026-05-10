# US-0006 · Assign Central user stories to developers

## Metadata
- Kind: `feature`
- Category: `workflow`

## Objective
## SpecForge Goal Intake

- Goal: `GOAL-SPECFORGE-CENTRAL`
- Strategy: `small-user-stories`
- Sequence: `6` of `8`
- Coding policy: do not implement directly from the broad goal; drive this story through SpecForge SDD phases before code changes.

## Original Goal

Develop SpecForge Central as an optional central service for teams using SpecForge clients. Central keeps user stories, workflow states, prompts and rules aligned across developers, supports versioned centralized configuration distribution, lets administrators create and assign user stories, lets client-created user stories sync back and become assigned to the creating developer, protects central configuration with a password or equivalent authorization so developers cannot bypass company rules, and supports offline client work with later synchronization.

## User Story Slice

As a team lead, I want to assign a Central user story to one or more developers so that each connected client can download only the work that belongs to its developer identity.

## MVP Slice

- Outcome: Developers receive the right Central work queue.
- Slice rationale: This is one small independently reviewable increment for SpecForge Central.

## Acceptance Intent

- A Central user story can be assigned to a developer identity.
- The client can list stories assigned to its configured developer identity.
- Unassigned or differently assigned stories are not downloaded as active local work by default.

## Non Goals

- Capacity planning or scheduling.
- Complex multi-team permissions.

## Dependencies

- US-0001
- US-0005

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
