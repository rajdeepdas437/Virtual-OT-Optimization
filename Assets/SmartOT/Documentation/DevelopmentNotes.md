# SmartOT — Development Notes (v2 — Debug & Fix)

## Folder Structure

```
Assets/SmartOT/
├── Scripts/
│   ├── Core/
│   │   ├── TaskData.cs               – Serializable data class for a single task
│   │   ├── TaskSequence.cs           – ScriptableObject: ordered list of tasks
│   │   └── TaskProgressionManager.cs – Runtime manager; singleton; validates & advances tasks
│   ├── Input/
│   │   └── SmartOTInputHandler.cs    – Wraps the existing Use InputAction; broadcasts events
│   ├── Interactions/
│   │   └── ProximityInteractor.cs    – Trigger-based proximity detector; raises interaction event
│   ├── Equipment/
│   │   └── EquipmentInteraction.cs   – Reusable activate/deactivate + task completion component
│   ├── Patient/                      – (empty for Stage 1; patient state scripts go here later)
│   └── UI/
│       ├── TaskUIManager.cs          – Displays phase / task / prompt / completed-count
│       ├── TaskListUI.cs             – Visual checklist with ✓/▶/[ ] icons per task (NEW v2)
│       └── ProximityUIBridge.cs      – Bridges ProximityInteractor → TaskUIManager
├── Prefabs/
├── Materials/
├── ScriptableObjects/
│   └── SmartOT_MainTaskSequence.asset – The TaskSequence asset (fixed in v2)
└── Documentation/
    └── DevelopmentNotes.md
```

---

## Bug Report — v2 Fixes

| Bug | Root Cause | Fix |
|-----|-----------|-----|
| **B1** – Task count always 0/3, no progression | `_taskId` was **empty** on all 3 EquipmentInteraction components | Must be set in Inspector — instructions below |
| **B2** – Input not reaching equipment | `_inputHandler` was **null** on all 3 ProximityInteractor components | Must be assigned in Inspector — instructions below |
| **B3** – Prompt never showed via UIBridge | `_taskUIManager` was **null** on all 3 ProximityUIBridge components | Must be assigned in Inspector — instructions below |
| **B4** – Patient Monitor task never matched | taskName had a leading **tab character** (`\t`) in the .asset file | Fixed in `SmartOT_MainTaskSequence.asset` directly + runtime Trim() added |
| **B5** – Task name text invisible | `taskDescription` was **empty** in the .asset; `RefreshUI()` used description not name | Fixed in asset + `RefreshUI()` now uses `taskName` as primary display |
| **B6** – Phase always 0 | `phase` was **0** in the .asset for all tasks | Fixed in asset: phase 1 and 2 set correctly |
| **B7** – Trigger never fires | Sphere collider radius was **0.012 m** (too tiny) | Inspector fix required: set radius to 1.5 m |
| **B8** – UI missed first task event | `TaskUIManager.Start()` subscribed **after** `TaskProgressionManager.Start()` fired the event | Fixed via `DefaultExecutionOrder(-10/-5)` + `TaskUIManager` pulls state directly in `Start()` |
| **B9** – Prompt stays after pressing E | No code called `HidePrompt` on successful interaction | Fixed: `EquipmentInteraction.Activate()` calls `ProximityInteractor.ForceHidePrompt()` |
| **B10** – No visual task list | Feature didn't exist | Added `TaskListUI.cs` |

---

## Purpose of Each Script

| Script | Namespace | Responsibility |
|--------|-----------|----------------|
| `TaskData.cs` | `SmartOT.Core` | Plain data: taskName, taskDescription, phase. |
| `TaskSequence.cs` | `SmartOT.Core` | ScriptableObject holding the ordered `TaskData[]` array. |
| `TaskProgressionManager.cs` | `SmartOT.Core` | Singleton. Validates task order, fires events, exposes `GetTaskName(i)`. `DefaultExecutionOrder(-10)`. |
| `SmartOTInputHandler.cs` | `SmartOT.Input` | Subscribes to existing `Player/Use` action. Raises `OnUseStarted/Performed/Cancelled`. |
| `ProximityInteractor.cs` | `SmartOT.Interactions` | Trigger zone. When player enters, subscribes to Use input. Added `ForceHidePrompt()`. |
| `EquipmentInteraction.cs` | `SmartOT.Equipment` | On interaction: validates task, activates, calls `ForceHidePrompt()` + `CompleteTask()`. |
| `TaskUIManager.cs` | `SmartOT.UI` | Subscribes to manager events. Updates HUD text. `DefaultExecutionOrder(-5)`. |
| `TaskListUI.cs` | `SmartOT.UI` | Renders per-task checklist rows with ✓/▶/[ ] icons. `DefaultExecutionOrder(-4)`. |
| `ProximityUIBridge.cs` | `SmartOT.UI` | Forwards proximity enter/exit → `TaskUIManager.ShowPrompt/HidePrompt`. |

---

## Execution Order (critical)

```
Frame 1 — Awake():
  TaskProgressionManager  (order -10)  → sets Instance, trims task names, logs
  TaskUIManager           (order  -5)  → validates TMP refs, logs warnings
  TaskListUI              (order  -4)  → will subscribe in Start

Frame 1 — Start():
  TaskProgressionManager  (order -10)  → fires OnNextTaskStarted(task[0])
  TaskUIManager           (order  -5)  → subscribes to events, calls RefreshUI directly
  TaskListUI              (order  -4)  → subscribes, calls Refresh directly
```

---

## Dependencies Between Scripts

```
SmartOTInputHandler
        │ OnUseStarted
        ▼
ProximityInteractor ──────────────────────────────────────► ProximityUIBridge
        │ OnInteractionTriggered          OnPlayerEntered/Exited      │
        ▼                                                             ▼
EquipmentInteraction                                         TaskUIManager
        │ ForceHidePrompt() ──────────────────────────────────────────▲
        │ CompleteTask()
        ▼
TaskProgressionManager
        │ OnNextTaskStarted / OnTaskCompleted / OnAllTasksCompleted
        ├──────────────────────────────────────────────────► TaskUIManager
        └──────────────────────────────────────────────────► TaskListUI
```

---

## ScriptableObject — SmartOT_MainTaskSequence

Path: `Assets/SmartOT/ScriptableObjects/SmartOT_MainTaskSequence.asset`

| Index | taskName (must match EquipmentInteraction._taskId exactly) | taskDescription | phase |
|-------|-------------------------------------------------------------|-----------------|-------|
| 0 | `Activate Anesthesia Machine` | Approach the Anesthesia Machine and press E to switch it on. | 1 |
| 1 | `Activate Patient Monitor` | Approach the Patient Monitor and press E to switch it on. | 1 |
| 2 | `Activate Laparoscopy Tower` | Approach the Laparoscopy Tower and press E to activate imaging. | 2 |

---

## Debug Log Reference

| Log ID | Message prefix | Meaning |
|--------|---------------|---------|
| LOG 1 | `[TaskProgressionManager] Task sequence initialized` | Awake: SO loaded, names trimmed |
| LOG 2 | `[TaskProgressionManager] Starting. First task:` | Start: event fired |
| LOG 3 | `[SmartOTInputHandler] Use action — STARTED` | E or A pressed |
| LOG 3b | `[ProximityInteractor] Use action received` | Forwarded by proximity zone |
| LOG 4 | `[EquipmentInteraction] HandleInteraction called` | Interaction pipeline entered |
| LOG 5 | `[EquipmentInteraction] activation starting` | Guards passed |
| LOG 6 | `[EquipmentInteraction] activated successfully` | Visual + sound played |
| LOG 7 | `[EquipmentInteraction] Calling CompleteTask` | Sent to manager |
| LOG 8 | `[TaskProgressionManager] CompleteTask called` | Manager received id |
| LOG 9 | `[TaskProgressionManager] Task completed` | Index incremented |
| LOG 10 | `[TaskUIManager] UI refreshed` | Text fields updated |

---

## How to Extend

### Add more tasks
1. Open `SmartOT_MainTaskSequence` asset → increase Tasks array → fill new entry.
2. Set `_taskId` on a new equipment's `EquipmentInteraction` to match `taskName` exactly.
3. No C# changes needed.

### Add grab-required interactions (Stage 2+)
- Create `Scripts/Interactions/GrabInteraction.cs`.
- Check `Autohand.Hand.GetHeldGrabbable()` to verify what is held.
- Call `TaskProgressionManager.Instance.CompleteTask(id)` on success.

### Add patient state changes (Phase 3)
- Create `Scripts/Patient/PatientStateController.cs`.
- Subscribe to `TaskProgressionManager.OnTaskCompleted`.
- Swap mesh / animator for inflated model.
