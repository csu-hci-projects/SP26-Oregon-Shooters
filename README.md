# Hand Motion Performance in Virtual Reality

This project explores how hand dominance affects performance in virtual reality (VR) when interacting with targets under different motion conditions. Participants complete a sequence of target selection tasks using both their dominant and non-dominant hands while targets remain stationary or move at different speeds.

This system was designed and implemented on Windows in Unity for the Meta Quest 3 headset as part of **CS 465: Multimodal Interaction for 3D Interfaces** at Colorado State University.

---

## Overview

Target selection and pointing are fundamental interaction techniques in VR systems. Although many studies evaluate static target acquisition, few consider how hand dominance affects performance when targets move dynamically within a 3D immersive environment.

This experiment compares user performance across:

* Dominant-hand interaction
* Non-dominant-hand interaction
* Static targets
* Slow-moving targets
* Fast-moving targets

The experiment records timing and accuracy data to analyze how motion and hand dominance influence interaction efficiency.

---

## Experimental Procedure

Participants wear the Meta Quest 3 headset and use a controller to point at and select targets.

The experiment is divided into two blocks:

1. Dominant-hand trials
2. Non-dominant-hand trials

Within each block, participants complete trials under three motion conditions:

* Static
* Slow-moving
* Fast-moving

Targets appear in front of the participant at varying positions. Participants attempt to select the target as quickly and accurately as possible using the controller trigger.

---

## Recorded Measurements

The system automatically logs:

* Trial number
* Motion condition
* Hand condition
* Completion time
* Hit or miss result
* Target coordinates
* Controller interaction data

Results are exported as CSV files for later analysis.

---

## Development Environment

The project was developed using:

* Unity 6
* Universal Render Pipeline (URP)
* LaTeX (ACM article template)
* C#
* OpenXR
* XR Interaction Toolkit
* Meta Quest 3

---

## Running the Project

1. Open the Unity project.
2. Load the experiment scene.
3. Connect the Meta Quest 3 headset.
4. Enter Play mode or build directly to the headset.
5. Ensure both controllers are held.
6. Follow the on-screen instructions to begin each block.

---

## Controls

| Action | Input |
|---|---|
| Aim | Controller orientation |
| Select target | Trigger button |
| Begin experiment block | Spacebar or controller trigger |

---

## Output Files

After completing the experiment, the application outputs a CSV file containing trial-level and summary statistics.

These files include:

* Movement times
* Accuracy rates
* Condition labels
* Aggregate performance metrics

---

## Motivation

Understanding how users perform under different motion conditions can help improve interaction design in VR systems. The findings from this project may be relevant to:

* VR interface design
* Accessibility and ergonomics research
* Motor-performance studies
* Training simulations
* Fast-paced VR applications

---

## Project Structure

* `Assets/` — Unity assets, scripts, scenes, and materials
* `Packages/` — Unity package dependencies
* `ProjectSettings/` — Unity configuration files
* `Docs/` — Reports, references, and supporting documents

---

## Course Information

**CS 465 – Multimodal Interaction for 3D Interfaces**  
Colorado State University  
Spring 2026