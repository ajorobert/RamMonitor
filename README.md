# RamMonitor

A tiny Windows tray app that watches **commit pressure** — the memory metric that actually predicts when Windows is about to slow to a crawl — and shows it as a single color-coded number in your system tray.

## Why this exists (and why most RAM monitors miss the point)

Most RAM monitors show **% of physical RAM used**. That number is almost useless for predicting Windows slowdowns, because Windows starts thrashing long before physical RAM is "full" — and sometimes when it looks comfortably empty.

The real metric is **commit charge**: the total memory all your apps have *promised* they might use, which Windows must back with either RAM or pagefile. When commit charge approaches the commit limit (RAM + pagefile), Windows is forced to extend the pagefile and then thrash it. That's when your machine becomes unresponsive and you end up restarting.

There's a second subtlety almost no monitor handles correctly: **the commit limit is not fixed**. On default Windows configurations, the pagefile grows dynamically as commit pressure rises. So a naive "% of commit limit" gauge can stay flat — or even *fall* — while the system is actively heading toward trouble, because the denominator inflated underneath you.

RamMonitor tracks both signals separately and encodes them into a single tray icon.

## What the icon shows

```
┌────┐
│ 24 │   ← Committed memory in GB
└────┘
```

One number. Two colors. Three pieces of information.

### The number — **Committed GB**

The amount of memory all running apps have committed. This is the value that moves throughout the day as apps allocate and release memory. Once you've run RamMonitor for a while, you'll learn your machine's normal range — and notice immediately when something is off.

### The text color — **how close to the current limit**

Tells you whether Windows is about to start thrashing *right now*:

| Color | Meaning |
|---|---|
| 🟢 Green | Committed < 70% of current limit — plenty of headroom |
| 🟡 Yellow | 70–85% — heavy load, consider closing apps |
| 🔴 Red | > 85% — danger zone, thrash imminent |

### The background color — **has Windows grown the pagefile?**

The leading indicator. Tells you whether Windows has been forced to extend its commit budget beyond what your machine was configured for:

| Color | Meaning |
|---|---|
| 🟢 Green | Commit limit at or near your baseline — normal |
| 🟡 Yellow | Limit has grown 2–15% above baseline — Windows is under real pressure |
| 🔴 Red | Limit grown > 15% — restart soon, even if the number color is still green |

**Why both colors matter:** the text color is the *concurrent* signal (you're full *now*). The background is the *leading* signal (Windows has already decided your baseline isn't enough). A red background with green text means your apps aren't currently maxed out, but Windows has quietly grown the pagefile to keep up — and once Committed catches up to the new inflated limit, you're done. Most monitors hide this case completely.

The tooltip on hover shows the exact numbers: `Committed X.X GB / Limit Y.Y GB`.

## Install & run

### Quick start (recommended)

1. Download or build `RamMonitor.exe` (see Build below).
2. Double-click to launch. The icon appears in the system tray.
3. **Make it visible in the taskbar** — see the next section.
4. Right-click the icon → **Settings…** → tick **Start with Windows** so it launches at every login.

### Pinning the icon to the visible tray

By default Windows hides new tray icons in the `^` overflow flyout. To pin it visible:

**Easiest — drag it out:**
1. Click the `^` arrow at the left edge of the tray to open the overflow flyout.
2. **Drag the RamMonitor icon out of the flyout and drop it onto the visible taskbar.** Done.

**Alternative — Settings:**
1. Make sure RamMonitor is running.
2. Settings → Personalization → Taskbar → "Other system tray icons" → flip **RamMonitor** on.
3. If it doesn't appear, toggle it off, wait a moment, and back on. (Windows 11 occasionally needs the nudge.)

### Settings

Right-click the tray icon → **Settings…**

- **Refresh interval** — how often to sample (15s–5min, default 30s). CPU cost is effectively zero at any setting.
- **Committed yellow / red** — % thresholds for the text color.
- **Limit yellow / red** — ratio thresholds for the background color (multiples of baseline).
- **Baseline limit (GB)** — your healthy commit limit. Captured automatically on first run. Edit manually, or click **Recalibrate** to set it to the current commit limit at any moment you know the system is healthy.
- **Start with Windows** — adds/removes a shortcut in your Startup folder. No admin required.

Settings are saved to `%APPDATA%\RamMonitor\settings.json`.

## How to read the gauge in practice

| What you see | What it means | What to do |
|---|---|---|
| Green text, green background | Healthy | Nothing |
| Yellow text, green background | Heavy memory use, within budget | Close a few apps if you plan to open more |
| Red text, green background | At capacity within your normal budget | Close apps now |
| Any text, yellow background | Pagefile has started growing — real pressure | Consider what's leaking; plan a restart |
| Any text, red background | Pagefile has grown significantly past baseline | Restart soon — you're on borrowed time |

The single most useful signal is **the background going yellow or red**. That's the thing other monitors hide from you, and it's the earliest reliable warning that a restart is in your near future.

## Resource cost

- Working set: ~30 MB
- CPU: rounds to 0% on any meter — one syscall every 30s
- No background services, no admin rights, no telemetry, no network

## Build from source

Requires .NET 10 SDK.

```powershell
git clone <this repo>
cd RamMonitor
dotnet publish RamMonitor -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output: `RamMonitor\bin\Release\net10.0-windows\win-x64\publish\RamMonitor.exe`

For a build that runs without requiring .NET 10 on the target machine, swap `--self-contained false` for `--self-contained true` (produces a larger ~70 MB exe).

## Uninstall

1. Right-click the tray icon → **Exit**.
2. Settings → **Start with Windows** off (or delete `RamMonitor.lnk` from `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`).
3. Delete `%APPDATA%\RamMonitor\` to remove saved settings.
4. Delete the exe.

No registry entries, no services, no scheduled tasks.
