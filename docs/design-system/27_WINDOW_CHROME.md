# Window Chrome

The prototype uses WPF `WindowChrome` with a 52 DIP caption and 6 DIP resize border. The command bar is the drag region; interactive menus and buttons opt back into hit testing.

Windows controls are lightweight `—`, `□`, and `×` buttons. Idle states are nearly transparent, hover reveals a surface, and close may use restrained semantic red. Commands map to real minimize, maximize/restore, and close behavior.

Evidence must cover 100%, 125%, 150%, and 200% logical scales. Close and page actions remain structurally separated; the prior close-safe-area contract is preserved.
