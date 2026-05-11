# US-0005 · Create and manage user stories in Central

## Metadata
- Kind: `feature`
- Category: `user-stories`

## Objective
## SpecForge Goal Intake

- Goal: `GOAL-SPECFORGE-CENTRAL`
- Strategy: `small-user-stories`
- Sequence: `5` of `8`
- Coding policy: do not implement directly from the broad goal; drive this story through SpecForge SDD phases before code changes.

## Original Goal

Develop SpecForge Central as an optional central service for teams using SpecForge clients. Central keeps user stories, workflow states, prompts and rules aligned across developers, supports versioned centralized configuration distribution, lets administrators create and assign user stories, lets client-created user stories sync back and become assigned to the creating developer, protects central configuration with a password or equivalent authorization so developers cannot bypass company rules, and supports offline client work with later synchronization.

## User Story Slice

As a product owner or team lead, I want to create user stories in SpecForge Central and track their workflow status so that Central becomes the team management hub for SpecForge work.

## MVP Slice

- Outcome: Central can act as the source of truth for team-visible user story inventory.
- Slice rationale: This is one small independently reviewable increment for SpecForge Central.

## Acceptance Intent

- Central can create a user story with title, kind, category, source text, and acceptance intent.
- Central stores and exposes each story current workflow status.
- Central-created stories can exist before they are assigned to a developer.

## Non Goals

- Full project portfolio reporting.
- Implementation of every workflow phase in Central.

## Initial Scope
- Includes:
  - ...
- Excludes:
  - ...
