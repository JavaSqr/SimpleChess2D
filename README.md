# ♟ Simple Chess 2D

<div align="center">

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black?style=for-the-badge&logo=unity)
![License](https://img.shields.io/badge/License-MIT-brightgreen?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-In%20Development-orange?style=for-the-badge)

</div>

---

This started as a Unity 2D chess **template** — something reusable, data-driven, no hardcoded rules. Then at some point I thought it'd be fun to give it actual polish, got inspired by [Balatro](https://www.playbalatro.com/)'s aesthetic, and here we are.

The good news: **it's still a template**. The visual layer sits completely on top. The underlying architecture — ScriptableObject configs, modular move patterns, event-driven game flow — is untouched. You can strip the visuals and use this as a starting point for your own chess variant without fighting the code.

---

## ✨ Features

🎨 &nbsp;**Visual**
- Balatro-inspired animated shader background
- Smooth board flip animation for local 2-player
- Drag-and-drop piece control with scale-up feedback
- Highlight system for valid moves, captures, check

⚙️ &nbsp;**Gameplay**
- Fully configurable pieces via ScriptableObjects — custom move patterns, no code needed
- Castling, en passant, pawn promotion (with piece selection UI)
- Per-team countdown timer
- Check, checkmate, and stalemate detection

🔧 &nbsp;**Technical**
- JSON save/load with multiple named slots
- Audio manager with sound priority queue
- Music crossfade between menu / game / game over
- Pause menu, game over screen, save/load panel

---

## 🗂 Project structure

```
Scripts/
├── Config/
│   └── Configs.cs              — ScriptableObjects: BoardConfig, PieceConfig, GameSetupConfig
├── Core/
│   ├── BoardGenerator.cs       — spawns cells, CellToWorld/WorldToCell with rotation support
│   ├── Cell.cs                 — single cell: visuals, highlight, click event
│   ├── Piece.cs                — piece component: drag, counter-rotation during board flip
│   └── PieceSpawner.cs         — spawns and manages all piece GameObjects
├── Data/
│   └── DataModels.cs           — serializable structs: SaveData, MoveRecord, GameState
├── Logic/
│   ├── BoardFlipper.cs         — rotates board, pieces counter-rotate to stay upright
│   ├── MoveValidator.cs        — move generation from MovePattern, check/mate detection
│   ├── SelectionHandler.cs     — click + drag input, executes moves, handles promotion
│   ├── TimerManager.cs         — per-team countdown
│   └── TurnManager.cs          — turn flow, fires events for move/capture/castling/check
├── Other/
│   └── AnimatorSupport.cs      — helper for triggering sounds from Animator
├── Save/
│   └── SaveManager.cs          — JSON save slots in Application.persistentDataPath
├── UI/
│   ├── UIManager.cs            — all panels: menu, HUD, pause, save/load, game over, promotion
│   └── ShaderController.cs     — smooth property transitions on the background shader
├── GameManager.cs              — central singleton, wires everything together
└── AudioManager.cs         — priority queue for sounds, crossfade music
```

---

## 🎮 Scene hierarchy

```
GameManager          — GameManager + SaveManager + AudioManager
Board                — BoardGenerator
Pieces               — PieceSpawner
Logic
  ├── MoveValidator
  ├── TurnManager
  ├── SelectionHandler
  ├── TimerManager
  └── BoardFlipper
Canvas               — UIManager
  ├── MainMenuPanel
  ├── HUDPanel
  ├── PauseMenuPanel
  ├── SaveLoadPanel
  ├── GameOverPanel
  └── PromotionPanel
Main Camera
```

---

## 🔩 Configuring pieces

Each piece type is a `PieceConfig` ScriptableObject (`Assets → Create → ChessTemplate → PieceConfig`).

Move behavior is entirely data-driven through `MovePattern` entries:

| Field | Description |
|---|---|
| `dRow` | Row delta per step. Flipped for team 1 automatically. |
| `dCol` | Column delta per step. |
| `applyDirectionToCol` | Also flip `dCol` for team 1. Use for pawn diagonal attacks. |
| `slide` | Repeat until blocked — rook, bishop, queen. |
| `canCapture` | Can land on an enemy piece. |
| `canMoveEmpty` | Can land on an empty cell. Set to false for pawn diagonals. |
| `firstMoveOnly` | Only usable on the piece's first move. Pawn double-advance. |

**Pawn setup (4 patterns):**

| dRow | dCol | applyDirectionToCol | canCapture | canMoveEmpty | firstMoveOnly |
|:----:|:----:|:---:|:---:|:---:|:---:|
| 1 | 0 | ✗ | ✗ | ✓ | ✗ |
| 2 | 0 | ✗ | ✗ | ✓ | ✓ |
| 1 | 1 | **✓** | ✓ | ✗ | ✗ |
| 1 | -1 | **✓** | ✓ | ✗ | ✗ |

Special flags:
- `isRoyal` — losing this piece triggers check/checkmate. King only.
- `canCastleWith` — can castle with the royal piece. Rook only.
- `canPromote` — triggers promotion on reaching the last rank. Pawn only.

---

## 🏗 Configuring the board

`BoardConfig` controls the visual side of the board — grid dimensions, cell size, light/dark colors or sprite overrides, highlight colors for selection/moves/captures/check, and border styling.

The board is always centered on the `Board` GameObject. Move the object to reposition everything.

---

## 📐 Starting layouts

`GameSetupConfig` defines the full starting state:

- Which `BoardConfig` to use
- Team definitions (name, color, index)
- `PiecePlacement` list — `PieceConfig` + `row` + `col` + `teamIndex`
- Rule toggles: `enableCastling`, `enableEnPassant`, `enablePromotion`

Team 0 starts at row 0 (bottom), team 1 at row 7 (top) for classic 8×8.

---

## ⚡ Special moves

**Castling** — `MoveValidator` checks that both king and rook are unmoved, the path is clear, and no traversed square is under attack. `SelectionHandler.ExecuteCastling` moves both pieces.

**En passant** — the validator tracks the last double pawn move in `LastDoublePawnMove`. The capturing pawn moves diagonally to an empty cell; `RemovePiece` clears the captured pawn at `enPassantCaptureRow/Col`.

**Promotion** — fires when a pawn with `canPromote` reaches the last rank. Input locks until the player picks a piece from `UIManager.ShowPromotionPanel`. No panel assigned → auto-promotes to the first entry in `promotionConfigs`.

---

## 🔄 Board flip

`BoardFlipper` rotates `boardTransform`. Each `Piece` calls `transform.rotation = Quaternion.identity` in `LateUpdate` to stay visually upright regardless of what the board is doing. `CellToWorld` and `WorldToCell` both factor in `transform.rotation`, so clicks and drag hit the right cells at any angle.

Two modes — automatic on turn change (`autoFlipOnTurn`), or via a manual button. `flipDuration = 0` skips the animation.

---

## 🔊 Audio priority

When multiple events fire in one frame (move + check, for example), only the highest-priority sound plays. Everything goes through `RequestSound` and resolves in `LateUpdate`:

| Priority | Event |
|:---:|---|
| 0 | Checkmate |
| 1 | Check |
| 2 | Capture |
| 3 | Castling |
| 4 | Move |
| 5 | Game start |
| 6 | Game over |
| 7 | UI |

UI sounds (`PlayUIClick`, `PlayUIOpen`) skip the queue and play immediately on a separate source.

Music crossfades between two `AudioSource`s using a coroutine. Call `PlayMenuMusic` / `PlayGameMusic` / `PlayGameOverMusic` from `GameManager` at the right moments.

---

## 💾 Saves

JSON files land in `Application.persistentDataPath/saves/`.

| Platform | Path |
|---|---|
| Windows | `%AppData%/../LocalLow/<Company>/<App>/saves/` |
| macOS | `~/Library/Application Support/<Company>/<App>/saves/` |
| Android / iOS | App sandbox |

Each save stores the full board state, move history, current turn, and timer budgets. Multiple named slots supported.

> ⚠️ `pieceId` in `PieceConfig` is the JSON key for piece type. Renaming it breaks existing saves.

---

## 🎨 Assets

All assets in this project — music, sprites, sound effects, and UI — are either **free to use** or **made by me**.

One exception: the animated background shader is taken directly from **Balatro**. It's included here purely for personal/educational use. If you're building something commercial, swap it out.

---

## 🛠 Requirements

- Unity 2021.3 LTS or newer
- TextMeshPro — `Window → TextMeshPro → Import TMP Essentials`
- No external packages