# Todo List for Macro Deck

A minimalist todo list for [Macro Deck](https://macro-deck.app). Add a todo from anywhere with a global
shortcut, manage your list from a small popup next to the menu bar or tray icon, and see or complete todos
right on your Macro Deck with a widget, a list modal, events, variables and actions.

This is a community plugin. It is not developed or endorsed by the Macro Deck project.

## Features

- **Macro Deck widget** that adapts to its size: a count with an overdue badge on one cell, a short list
  on 2x1 and 1x2, and a paged list with complete buttons on 2x2 and larger.
- **List modal** on any Macro Deck screen: add, complete, reopen, edit (title, notes, due date) and delete
  todos, with tabs for open, due and completed todos.
- **Actions** to create, complete, reopen and delete todos and to open the list, usable in action flows
  with Macro Deck variables.
- **Events** when a todo is created, updated, completed, reopened, deleted, becomes due or becomes overdue.
- **Variables** with counts and the next todo, for button labels and conditions.
- **Desktop popup** next to the macOS menu bar item or the Windows notification area icon, fully usable
  from the keyboard.
- **Global quick-add shortcut** (default Ctrl+Alt+Shift+T, on macOS ⌃⌥⇧T) that opens a small input,
  optionally with a due date.
- Light and dark mode following the system, or fixed in the settings.
- **English and German.** Macro Deck shows the plugin in each screen's language; the desktop windows
  follow the operating system's language. Other languages fall back to English.

All of it works on one shared list: a todo completed on the deck disappears from the popup at once, and
the other way round.

## Requirements and platforms

- Macro Deck 3 on **Windows 10/11 (x64)** or **macOS on Apple Silicon**.
- macOS on Intel and Linux are not supported in this version. Macro Deck ships for macOS on Apple Silicon.
- No account, no internet connection and no extra software. The plugin never talks to the network.

## Due and overdue

Every todo has at most one deadline. Two thresholds, set in the desktop settings, turn it into a state:

| State | When |
| --- | --- |
| Open | Not completed, and either no deadline or the deadline is more than *due soon* away |
| Due | Not completed, from *due soon* before the deadline (default 60 minutes) until the deadline plus *overdue after* |
| Overdue | Not completed, from the deadline plus *overdue after* (default 0 minutes) on |
| Completed | Marked as done; never due or overdue |

"Open" counts include due and overdue todos. With both thresholds at 0 a todo goes straight from open to
overdue at its deadline. The same rules drive the widget, the modal, the desktop popup, the variables and
the events.

Due and overdue events fire once per deadline, even across restarts. If Macro Deck was not running when a
todo became due or overdue, the plugin raises the latest state once when Macro Deck starts: an overdue
todo raises only *Todo overdue*, not also *Todo due*. Changing the deadline or reopening a completed todo
starts a new cycle. Deadlines are stored in UTC, so daylight saving changes and time zone changes do not
move them; the plugin re-checks at least every five minutes, which also covers sleep and resume.

## Using it in Macro Deck

### Widget

Add the **Todo list** widget to a deck. Tapping it opens the list modal.

| Size | Shows |
| --- | --- |
| 1x1 | Number of open todos, overdue count |
| 2x1 | Up to three todos with due labels |
| 1x2 | Up to four todos, title above the due label |
| 2x2 and larger | A page of todos with complete buttons; the button at the top right pages through the list |

Settings: visible todos, sort order (due date, newest first, manual), which todos to show (all, due
soon, overdue, either), show due dates, overdue first, show completed
todos, and complete buttons on large widgets.

While a large widget shows complete buttons, tapping it pages through the list instead of opening the
modal. This is how Macro Deck widgets work in the current SDK: once a widget has its own controls, the
tile's tap belongs to them. Turn off *Complete buttons on large widgets* to make the tap open the list, or
use a smaller widget or the *Open todo list* action for that.

### List modal

Opened by tapping a widget or by the *Open todo list* action. Type a title and press Enter to add a todo.
Tap the circle to complete or reopen, tap a title to edit it, tap the bin twice to delete.

The current Macro Deck UI has no date picker and single-line text fields only, so a due date is typed or
picked from presets (in 1 hour, this evening, tomorrow, next week, no date) and shifted by a day or an hour.
Typed due dates accept `2026-10-12 18:30`, your short date and time format, a time like `18:30` (today,
or tomorrow if already past), a date alone (09:00), an offset like `+30m`, `+2h`, `+3d`, `+1w`, or words in
English or German: `today`, `tomorrow 9`, `fri 18:00`, `next week`, `morgen 9 Uhr`, `20.10. 14:30`, `6pm`.
A day without a time means 09:00, `today` alone means 18:00. Keyboard
support is limited to Enter in text fields and Escape to close. Modals cannot open on hardware decks.

### Actions

| Action | Parameters | Notes |
| --- | --- | --- |
| Create todo | Title, notes, due date | Variables in all three are filled in by Macro Deck. A templated due date may use any form the modal accepts. |
| Complete todo | Todo (optional) | Without a todo it completes the next one: the one `todo_next_title` shows. Fails with *not found* when nothing is open. |
| Reopen todo | Todo | Reopening an open todo succeeds and changes nothing. |
| Delete todo | Todo | Deletes permanently. |
| Open todo list | | Opens the modal on the screen that ran it. Fails from automations and hardware decks. |
| Open desktop todo list | | Opens the popup on the computer running Macro Deck. Fails when the desktop UI is not running. |

### Events

`Todo created`, `Todo updated`, `Todo completed`, `Todo reopened`, `Todo deleted`, `Todo due`, `Todo overdue`.
Each carries `todoId`, `title`, `description`, `dueAt` (ISO 8601 in UTC, empty without a deadline) and
`status` (`open`, `due`, `overdue` or `completed`). Completing and reopening raise only their own event.
Events reach Macro Deck while it is connected. Due and overdue transitions that happen while it is not
connected are held and raised once it reconnects; other events from that time (for example a todo added
from the desktop popup) are not replayed.

### Variables

| Variable | Value |
| --- | --- |
| `todo_open_count` | Todos that are not completed (including due and overdue) |
| `todo_completed_count` | Completed todos |
| `todo_due_count` | Due todos |
| `todo_overdue_count` | Overdue todos |
| `todo_total_count` | All todos |
| `todo_next_title` | The open todo with the earliest deadline (overdue included), otherwise the first open todo |
| `todo_next_due_at` | Its deadline, ISO 8601 in local time |
| `todo_next_due_in_minutes` | Minutes until that deadline; negative when overdue |

Use them as `{{ vars.todo_open_count }}`. Variables without a value (for example the next deadline when no
open todo has one) are *not available*. Macro Deck reads the values every five seconds, so a variable can
lag a change by up to five seconds; the widget and the modal update immediately.

## Desktop popup and quick add

The plugin adds a checklist icon to the macOS menu bar or the Windows notification area while Macro Deck
runs. Clicking it opens a compact popup next to the icon; it closes when you click elsewhere or press
Escape. On Windows the icon's right-click menu also offers quick add and settings.

| Key | In the popup |
| --- | --- |
| Up / Down | Move between the input and the todos |
| Enter | Add the typed todo, or edit the selected one (Enter saves the edit) |
| Space | Complete or reopen the selected todo |
| Delete / Backspace | Delete the selected todo (asks first unless turned off) |
| Ctrl+N (Windows), Cmd+N (macOS) | Jump to the input |
| Escape | Cancel the edit or deletion, or close the popup |
| Tab / Shift+Tab | Move between controls |

Right-click a todo for edit, complete, delete and, with manual sorting, move up and down.

The global shortcut opens a smaller window with a title and a due date dropdown. Tab moves to the
dropdown, Enter opens it. Its last entry, Custom, opens a field below that takes the same
words as above, steps through suggestions with the arrow keys and offers a calendar for the mouse; a
preview shows what it means. Enter in the title or the custom date, or Command+Enter (Ctrl+Enter on Windows) anywhere, adds the todo
and closes the window;
Escape cancels. The shortcut can be changed or turned off in the settings, which also show
when another application already uses it. On macOS, letter shortcuts refer to the key positions of a US
keyboard, and shortcuts that only use Option are not allowed by macOS.

Settings (button in the popup): shortcut, theme, default sort, default due date for new todos, due soon
and overdue after thresholds, showing completed todos, closing the popup on focus loss, remembering the
popup's state, confirming deletions and the data folder.

The shortcut is registered with `RegisterHotKey` on Windows and `RegisterEventHotKey` on macOS. The
plugin installs no keyboard hook, does not monitor input and needs no Accessibility or Input Monitoring
permission.

If the desktop UI cannot start (for example without a desktop session), the plugin keeps working in Macro
Deck without it; the settings are then not available and the defaults apply. Only one Macro Deck process
shows the desktop UI at a time.

## Data and privacy

Todos and settings are stored in a SQLite database (`todos.db`) in the plugin data directory Macro Deck
provides, which survives plugin updates. The settings can move it to another folder, for example a synced
one; the current todos are copied there, or, if the folder already holds todos, you choose between using
those and replacing them (the replaced file is kept as a `.bak`). The previous file stays as a copy. Before a schema upgrade the database is copied to
`todos.db.v<old version>.bak` next to it. Nothing is sent anywhere; the plugin makes no network
connections.

## Limitations

- The widget cannot scroll (Macro Deck widgets are fixed tiles); large widgets page instead.
- Large widgets with complete buttons cannot open the modal on tap (see *Widget*).
- The modal has no date picker, no multi-line notes and no keyboard navigation between buttons.
- Variables are polled by Macro Deck every five seconds.
- macOS on Intel and Linux are not supported.

## Development

Requires the .NET 10 SDK. Avalonia's build tooling would send anonymous build statistics;
`Directory.Build.targets` turns that off for every build, including the release workflow.

```bash
dotnet build -c Release -warnaserror
```

```bash
dotnet test -c Release
```

The plugin CLI (`dotnet tool install --global MacroDeck.Plugin.Cli --version 3.0.0-beta.16`) runs the
conformance suite, renders widget previews and packs releases:

```bash
macrodeck-plugin test --project src/TodoList.Plugin
```

```bash
macrodeck-plugin preview render --project src/TodoList.Plugin --cells 1x1 --cells 2x1 --cells 1x2 --output ../previews
```

The desktop popup, quick add and settings window render to PNG, in dark and light, with sample data:

```bash
dotnet run --project tools/TodoList.DesktopPreview -c Release -- ../desktop-previews
```

The preview renderer cannot select the 2x2 layout; the *Large layout* previews render it directly
(`--size 252x252`).

```bash
macrodeck-plugin build --source src/TodoList.Plugin --output ./artifacts
```

Project layout and coding rules are in [AGENTS.md](AGENTS.md). `TodoList.Core` holds the todo logic and
storage, `TodoList.Desktop` the Avalonia UI, and `TodoList.Plugin` the Macro Deck integration and the
entry point, which runs Macro Deck's plugin host on the thread pool and the desktop UI on the main thread.

### Run and debug against Macro Deck

The project contains exactly one interactive launch profile: **Macro Deck - Real Host**. It launches
the plugin project directly, so Rider and Visual Studio attach the debugger to plugin code without a
wrapper or child-process attach. The profile connects in self-registering mode to the installed Macro
Deck desktop app at `http://127.0.0.1:8193`.

For the first run:

1. Start Macro Deck.
2. Open **Developer Tools → Plugin tokens**, create a token and copy it. It is shown only once.
3. Store the token in the source project's **.NET User Secrets** using one of the methods below. The
   project is already initialized; do not run `dotnet user-secrets init`.
4. Select **Macro Deck - Real Host** and start it with **Debug**.
5. Once enrollment succeeds, remove the token from User Secrets.

#### Set the token in Rider or Visual Studio

In Rider, right-click `TodoList.Plugin` in the Solution Explorer and select
**Tools → .NET User Secrets**. In Visual Studio, right-click the same source project and select
**Manage User Secrets**. Do not select the `.Tests` project.

The IDE opens a `secrets.json` file stored in your user profile, outside this repository. Replace its
contents with:

```json
{
  "MacroDeck:Plugin:EnrollmentToken": "<paste the one-time token here>"
}
```

Save the file, then start **Macro Deck - Real Host**. After enrollment, reopen `secrets.json` and
remove the `MacroDeck:Plugin:EnrollmentToken` entry.

#### Set the token from a terminal

From the repository root on macOS or Linux, use the following form. It reads the token without echoing
it and does not put the value in shell history or process arguments:

```bash
project="src/TodoList.Plugin/TodoList.Plugin.csproj"
printf "Enrollment token: "
read -rs md_enrollment_token
printf '\n'
printf '{"MacroDeck:Plugin:EnrollmentToken":"%s"}\n' "$md_enrollment_token" |
  dotnet user-secrets set --project "$project"
unset md_enrollment_token
```

After the first successful profile launch, remove the one-time token:

```bash
dotnet user-secrets remove "MacroDeck:Plugin:EnrollmentToken" --project "$project"
```

With PowerShell 7, use the equivalent masked-input form:

```powershell
$project = "src/TodoList.Plugin/TodoList.Plugin.csproj"
$token = Read-Host "Enrollment token" -MaskInput
@{ "MacroDeck:Plugin:EnrollmentToken" = $token } |
  ConvertTo-Json -Compress |
  dotnet user-secrets set --project $project
Remove-Variable token
```

Then remove it after enrollment:

```powershell
dotnet user-secrets remove "MacroDeck:Plugin:EnrollmentToken" --project $project
```

The profile persists the exchanged plugin credential under
`src/TodoList.Plugin/.macrodeck-dev-state/`, which is ignored by Git. Later profile launches
reuse that credential.

User Secrets are local-only but not encrypted. Never put the enrollment token in `launchSettings.json`,
a shared IDE configuration, a literal command argument or a commit. If you intentionally clear the
local state, create a fresh token and repeat the User Secrets step. Self-registration only works
against a host on the same machine. See the official
[Rider User Secrets guide](https://www.jetbrains.com/help/rider/Manage_NET_user_secrets.html) and
[.NET Secret Manager guide](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0)
for more background.

### Third-party notices

`THIRD-PARTY-NOTICES` covers every package in the published output for both platforms. After changing
dependencies, publish both platforms (`macrodeck-plugin build`), draft the per-platform notices with the
Macro Deck plugin creator's `inventory-dependencies.py`, and merge them:

```bash
python3 eng/merge-notices.py <notices-win-x64.txt> <notices-osx-arm64.txt> ~/.nuget/packages/skiasharp.nativeassets.macos/<version>/THIRD-PARTY-NOTICES.txt THIRD-PARTY-NOTICES
```

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in
[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES).
