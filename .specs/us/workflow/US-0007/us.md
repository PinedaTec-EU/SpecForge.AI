# US-0007 · Sync client-created user stories to Central

## Metadata
- Kind: `feature`
- Category: `workflow`

## Objective
## SpecForge Goal Intake

- Goal: `GOAL-SPECFORGE-CENTRAL`
- Strategy: `small-user-stories`
- Sequence: `7` of `8`
- Coding policy: do not implement directly from the broad goal; drive this story through SpecForge SDD phases before code changes.

## Original Goal

Develop SpecForge Central as an optional central service for teams using SpecForge clients. Central keeps user stories, workflow states, prompts and rules aligned across developers, supports versioned centralized configuration distribution, lets administrators create and assign user stories, lets client-created user stories sync back and become assigned to the creating developer, protects central configuration with a password or equivalent authorization so developers cannot bypass company rules, and supports offline client work with later synchronization.

## User Story Slice

As a connected SpecForge developer, I want a user story created locally in my client to be sent to SpecForge Central and automatically assigned to me so that local intake work becomes visible to the team without losing ownership.

## MVP Slice

- Outcome: Developer-originated stories flow back into Central with ownership preserved.
- Slice rationale: This is one small independently reviewable increment for SpecForge Central.

## Acceptance Intent

- When online and connected, a locally created story can be pushed to Central.
- Central records the creating developer as the assignee by default.
- The client records the Central identity or sync marker for the pushed story to avoid duplicate uploads.

## Non Goals

- Conflict resolution for simultaneous duplicate stories beyond duplicate upload prevention.
- Approval workflow for accepting submitted client stories.

## Dependencies

- US-0005
- US-0006

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
