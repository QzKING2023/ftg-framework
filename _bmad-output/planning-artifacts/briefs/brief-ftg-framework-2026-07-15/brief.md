---
title: "Product Brief: FTG Framework"
status: final
created: 2026-07-15
updated: 2026-07-15
---

# Product Brief: FTG Framework

## Executive Summary

**FTG Framework** is a Godot 4.x C# low-level framework built for fighting game developers. It does not make "a fighting game" — it provides the skeleton every fighting game needs: input system, frame data engine, combo mechanics, character state machine, hitbox system — and a full set of training-mode visualization UI components, out of the box. Developers grow their own muscle on top: character moves, art, balance.

Existing Godot fighting game solutions lack two critical things: a transparent frame data system (the "I can see what happened on every frame" experience of SF6 training mode), and an input buffer/leniency mechanism that is flexible and precise enough. FTG Framework fills both gaps, and bundles in combo systems (Gatling/cancel chains), replay, and object pooling — the wheels every fighting game developer eventually rewrites.

Its ambition is universal: from the grounded footsies of Street Fighter to the high-speed aerial chains of BlazBlue/UNI, the same foundation carries them all. Open source. Local-only. No networking. Passion-driven, built for the long haul.

## The Problem

The first six months of making a fighting game are joyful: characters move, animations play, basic collisions work. The problems start in month seven.

The developer wants a training mode — a place where players can see frame data for every attack, understand why they got hit, and practice combo timing. She realizes she needs to maintain two independent timelines — button press history and directional input history — without one interfering with the other. She needs to calculate startup frames, active frames, recovery frames, hit advantage, and knockdown advantage for every move. She needs to track cancel windows and actionable windows. The data itself is not complex. But turning it into visual components on a training mode UI — that is where things spiral out of control.

Then comes the input system. Simple "down + P = fireball" works fine until someone actually plays the game seriously. 623 must be input with frame-perfect precision to get a DP — no SF4-style leniency for 636 or 323. The button window mid-combo is only 2-3 frames, with no 6f buffer to make links realistically executable through muscle memory. Charge moves lose their charge the instant the direction is released — no 5f charge retention to lubricate combo routing.

And then: combo systems, replay, object pools, hitbox rendering... every fighting game developer independently solves the same infrastructure problems. This is not core gameplay — this is plumbing. And nobody has built it properly in Godot.

Plenty of fighting games have frame data visualization. Very few match the completeness, transparency, and usability of Street Fighter 6's training mode. Studios with big budgets can afford it; indie developers and small teams make do with less. At the most basic level — someone wanting to build a frame data meter mod for an existing game — there is not even a reference codebase worth studying.

## The Solution

Clone the repo, install the framework, open a scene — the training mode UI is already running. A demo character stands across the screen. Press P to punch. The frame data panel appears on the side: startup, active frames, recovery, frame advantage on hit for both characters. Historical input log scrolls at the bottom. Pause. Step forward frame by frame. Rewind. Watch the hitbox overlay rendered on the character at that exact moment. This is the first surprise FTG Framework delivers: **frame data transparency and training mode, out of the box.**

The developer does not lose sleep over the input system. Directional input leniency — 623 accepts 636 and 323, 236236 accepts 23626, while basic 236/214 stay strict — is configured per move. The button buffer (~6f) and charge retention (~5f) make combo feel modern, forgiving, and trainable. She can focus on what actually drives her: character design, frame data design, balance tuning — the reasons she wanted to make a game in the first place.

Under the hood, the framework maintains a clean frame data engine (startup, active, recovery, hit/block differential, cancel windows), provides state machine construction tools with a basic example (physics parameters and movement handling), and includes Gatling table registration, cancel window mechanics, hitbox system, replay, and object pooling. Developers register moves and configure routes; the framework manages the rest.

What the framework **explicitly does not do**: a preset complete combo system (damage scaling calculations, infinite combo prevention, hitstun behavior chains — these are creative game design decisions, and the framework does not make them for you), character art and animation, game balance values, stage/level design.

## What Makes This Different

In the Godot ecosystem, FTG Framework has no direct competitor — not because others cannot build it, but because nobody has yet. The closest alternative is Unity's **UFE 2**, a commercial-grade fighting game framework. But UFE 2 is Unity-only, closed-source, and not free. Choosing UFE 2 means leaving the Godot ecosystem, or waiting for a port that does not exist.

If only one differentiator could survive — delete the training mode, delete replay, delete the object pool — what remains is the **input system**. Not because the other modules are unimportant, but because the input system sets the "feel" of a fighting game at its foundation. Directional leniency for complex specials (623 → 636/323, 236236 → 23626) removes the mental barrier of precise DP and super inputs for new players. The ~6f button buffer turns "0f links" from pro-player territory into combos ordinary players can stabilize through muscle memory. ~5f charge retention frees charge-character combo routing from "release and lose it" constraints. Together, these three things shift fighting game execution from "harsh precision" toward "modern accessibility."

Training mode visualization is the second card — the killer feature for developer experience. But if forced to keep only one, the input system is the heart; the rest are limbs.

This framework's moat is not a technical barrier — it is the combination of **"in Godot, open source, native C#, built from fighting game developer pain points."** It does not compete for Unity's user base. It serves developers who already chose Godot, and then discovered there was nowhere to go when they wanted to make a fighting game.

## Who This Serves

**The primary user** is a fighting game developer who chose Godot. She may have spent months on characters and animations, only to hit the infrastructure wall — input, frame data, training mode — and is deciding whether to push through or quit. She may also be starting a new project from scratch, wanting a foundation that lets her skip low-level rewrites before writing her first real gameplay line.

**The framework's first user is the author.** FTG Framework follows a simple design principle: dogfooding. The author uses this framework in his own fighting game projects. Any rough edges — verbose APIs, tedious configuration, unreasonable defaults — will surface and get fixed through real use. The framework's quality is driven by real projects, not imagined requirements.

The modding community is a welcome bonus. If someone in the future wants to build a frame data meter for an existing game, FTG Framework's source can serve as reference — but the framework is not designed for this scenario and makes no promises toward it.

## Success Criteria

- **Dogfooding pass (baseline)**: The author's fighting game project successfully migrates to run on FTG Framework. Characters function correctly under the framework's state machine and input system, the training mode displays frame data accurately, and combo feel matches expectations. This is the minimum viability threshold — if the author would not use it, no one else will.

- **External adoption (key milestone)**: An independent developer with no write access to the repository ships at least one playable fighting game using FTG Framework (itch.io demo, game jam entry, or public download). This proves the documentation, API, and setup flow are clear enough for a stranger to integrate independently.

- **Community contribution**: The framework receives a substantive PR from someone other than the author — not a typo fix, but a feature addition (e.g., new input preprocessing mode, additional hitbox type). This signals the code structure and contribution guidelines support collaboration.

- **Community recognition**: In FGC developer communities (Discord, Reddit, forums), someone spontaneously recommends FTG Framework as a starting point for Godot fighting games. Tutorials or videos created by someone other than the author appear.

- **Long-tail reference value**: The framework's source code (particularly the frame data engine and input system) is cited or referenced by at least one non-FTG-Framework user — a modder, a game reverse-engineering enthusiast, or the author of another framework.

## Scope

### V1 (First Release)

Three core modules only — the hardest, most painful parts of fighting game infrastructure to build yourself:

- **Input System**: Directional input leniency (per-move configuration), ~6f button buffer, ~5f charge retention. Dual-track input storage (buttons + directions), independent and non-interfering.
- **Frame Data Engine + Training Mode Visualization**: Move frame data calculation (startup, active, recovery, hit/block advantage, cancel windows), plus a complete set of training mode UI components (frame data panel, frame advantage display, historical input log, frame-by-frame playback, hitbox overlay).
- **Combo System API**: Gatling table registration and cancel window mechanics. The framework provides the API — developers define their own routes, judge cancel eligibility, and handle hit reactions. Damage scaling, infinite prevention, hitstun chaining, and other design logic are **outside the framework's scope**.

These three modules together constitute a shippable framework. They address the most critical pain points in fighting game development: feel, transparency, and combo infrastructure.

### Explicitly Deferred (Post-V1)

- Character state machine (V1 provides construction tools and a basic example only; full system deferred)
- Hitbox system
- Replay system
- Object pool

### Permanently Out of Scope

- Networking / online play. This is a local-only framework.
- Godot 3.x support. Godot 4.x only, using the bundled Mono runtime.
- Complete combo game logic (damage scaling, infinite prevention, hitstun chains — these belong to game design, not the framework).
- Character art, animation, game balance values, stage/level design.

## Vision

Three years from now, when a Godot developer asks in an FGC Discord "I want to make a fighting game, where do I start?", one of the replies will say: "Install FTG Framework. It saves you six months of reinventing wheels."

The framework is no longer just the author's passion project — it has a small, stable circle of contributors, and the post-V1 modules (state machine tools, hitbox system, replay, object pool) are complete. It has powered at least several independent fighting games in distinct styles: an SF-inspired footsies-focused title, a BlazBlue-style high-speed aerial combo game, and perhaps an experimental solo fighting game concept.

It will not become a commercial product like Unity's UFE — it does not need to. It is a small, sharp cornerstone that ensures fighting games in the Godot ecosystem no longer start from zero.
