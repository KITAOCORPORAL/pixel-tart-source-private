# Project / Shot / Visual Reference Relationship

```text
Project
├─ default palette
├─ default tone target
├─ default Project Look ──────────────┐
├─ publishing defaults               │ fallback
└─ Shots                              │
   ├─ Shot 01 ─ optional Look override┘
   │  ├─ lighting references
   │  ├─ pose references + execution status
   │  ├─ storyboard references
   │  ├─ styling references
   │  └─ notes
   └─ Shot 02 ...
```

Visual references and Shot references are non-owning. Library content is addressed by stable library/asset identity; external material uses a managed reference. Deleting a Look or Shot never deletes source assets.

`ProjectShotStore` persists one atomic, versioned catalog per project. `ProjectShotExecution` is a small UI-independent state machine for order, status, Pose advancement and Pin. Tether UI consumes it but does not become a planning editor.

Effective Look selection is `Shot.ReferenceLookId ?? ProjectDefaultLookId`. The current tether integration applies an explicit Shot override; project-default lookup remains a documented partial where no active project context is supplied by the current folder-monitor start screen.
