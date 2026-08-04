# Epic 1 手动验证操作文档

> **Status**: Complete

## CORR-1 superseding verification note

Historical limitations below remain historical evidence only. CORR-1 now provides production-path two-player movement and an art-free diagnostic presentation. Canonical controls are P1 `A`/`D`/`S`/`Space`/`U` and P2 `Left`/`Right`/`Down`/`Up`/`N`; block is facing-relative Back. `H`/`B` are gated TEST ONLY lifecycle diagnostics and cannot prove gameplay hit/block behavior. Use the joint Story 2.3 + CORR-1 acceptance plan for current manual verification.
> **状态**: 已完成并由 Q1625 于 2026-07-31 验收
> **创建日期**: 2026-07-30
> **最后更新**: 2026-08-01 (PREP-2.4 对齐已完成验证与验收证据)
> **适用版本**: v2-epic-1 (Story 1.0 ~ 1.3)
> **目标**: 在 Godot 编辑器中批量完成 Epic 1 所有故事的手动验证

---

## 前置条件

1. Godot 4.x (Mono 版) 已安装
2. Epic 1 验收回归 `dotnet test` 全部通过 (747/747); 本文保留早期 603/603 里程碑作为历史记录
3. 用 Godot 编辑器打开当前项目仓库 (无需 `ftg new`—框架项目本身可直接运行)

---

## 项目场景架构说明

**本项目的 `main.tscn` 和 `training.tscn` 并不存在** — 这是有意设计的。

实际架构:
- `project.godot` 中 `run/main_scene="res://node_2d.tscn"` — 这是一个空白 Node2D 场景, 仅用于承载 GameLoop autoload
- `GameLoop` 是 autoload 节点, 在 `_Ready()` 中初始化所有模块并启动 `SceneManager`
- `SceneManager` 使用 **代码工厂函数** (而非 `.tscn` 文件) 创建场景:
  - `"character_select"` → `new CharacterSelectScene { DataStore = ... }`
  - `"training"` → `new TrainingScene { DataStore = ..., InputHistory = ..., FrameDataEngine = ..., StateMachine = ... }`
- 启动流程: `node_2d.tscn` → GameLoop._Ready() → SceneManager.GoTo("character_select") → CharacterSelectScene.Enter()

**关于角色选择界面**: 由于美术资源未准备，`CharacterSelectScene` 现已配置为启动后**自动确认双方角色** (P1=Ryu, P2=Ken) 并直接跳转到训练场景。如需恢复手动选择, 删除 `CharacterSelectScene.Enter()` 中的 `CallDeferred(nameof(AutoStartMatch))` 调用即可。

### 角色选择界面 — 手动模式按键 (仅供恢复手动模式时参考)

| 玩家 | 浏览角色 | 确认选择 | 取消选择 |
|------|----------|----------|----------|
| P1 | `A` / `D` | `W` | `S` |
| P2 | `←` / `→` | `↑` | `↓` |

---

## 第一部分: Story 1.3 — 场景模板 Export 引用绑定 (Task 3.2)

### 目标
将 `character_template.tscn` 中所有节点的 Export 引用在 Inspector 中正确绑定, 使 CharacterController 脚本能在运行时找到子节点。

### 步骤

#### 1.1 打开场景
1. 启动 Godot 编辑器
2. 在 FileSystem 面板中, 导航至 `Characters/character_template.tscn`
3. 双击打开该场景

#### 1.2 选择根节点
1. 在 Scene 面板中, 点击根节点 `Character`
2. 在右侧 Inspector 面板中, 你应该看到以下 `CharacterController` 下的 Export 属性:

| 属性名 | 类型 | 默认值 |
|--------|------|--------|
| SpriteContainer | Node2D | (空) |
| HurtboxContainer | Node2D | (空) |
| AnimationPlayer | AnimationPlayer | (空) |
| PlayerId | int | 1 |
| CharacterId | string | "" |
| StateDebugLabel | Label | (空) |

#### 1.3 绑定节点引用
从 Scene 面板中 **拖拽** 子节点到 Inspector 对应属性槽:

1. **SpriteContainer** ← 拖拽 `SpriteContainer` 节点到槽位
2. **HurtboxContainer** ← 拖拽 `HurtboxContainer` 节点到槽位
3. **AnimationPlayer** ← 拖拽 `AnimationPlayer` 节点到槽位
4. **StateDebugLabel** ← 拖拽 `StateDebugLabel` 节点到槽位

> **验证**: 绑定后, Inspector 中每个槽位应显示绿色的 NodePath 图标, 表示引用已建立。如果出现红色图标, 说明引用丢失, 需要重新拖拽。

#### 1.4 保存场景
- 按 `Ctrl+S` 保存 `character_template.tscn`
- 这会将 Export 引用 ID 写入 `.tscn` 文件的 `[node]` 段中

---

## 第二部分: Story 1.3 — 全量编辑器验证 (Task 10)

### 目标
验证角色模板在运行时的完整行为: 实例化、状态机联动、输入响应、双玩家独立性、视觉更新。

### 2.1 验证场景节点结构 (AC-1, AC-10)

1. 打开 `Characters/character_template.tscn`
2. 确认 Scene 面板的节点树为:
   ```
   Character (Node2D, script=CharacterController)
   ├── SpriteContainer (Node2D)
   │   └── Sprite (Sprite2D)
   ├── HurtboxContainer (Node2D)
   │   ├── HeadHurtbox (Area2D)
   │   │   └── CollisionShape2D
   │   ├── BodyHurtbox (Area2D)
   │   │   └── CollisionShape2D
   │   └── LegsHurtbox (Area2D)
   │       └── CollisionShape2D
   ├── AnimationPlayer
   └── StateDebugLabel (Label)
   ```
3. 确认 Inspector 中 `CharacterController` 的所有 Export 属性均可见 (AC-7)

> **检查点**: 节点树结构与设计完全一致 = PASS

### 2.2 验证 Inspector Export 属性 (AC-7)

选中根节点 `Character`, 在 Inspector 中确认以下六个属性区域可见:

| 属性 | 验证内容 |
|------|----------|
| SpriteContainer | 类型为 Node2D, 已分配节点引用 |
| HurtboxContainer | 类型为 Node2D, 已分配节点引用 |
| AnimationPlayer | 类型为 AnimationPlayer, 已分配节点引用 |
| PlayerId | int, 默认值 1 |
| CharacterId | string, 默认值 "" |
| StateDebugLabel | 类型为 Label, 已分配节点引用 |

> **检查点**: 全部 6 个属性可见且 SpriteContainer、HurtboxContainer、AnimationPlayer、StateDebugLabel 已绑定 = PASS

### 2.3 验证运行时启动 (AC-3)

1. 直接按下 `F5` 或点击右上角 "Run Project" 按钮 (主场景 `node_2d.tscn` 会自动加载)
2. 启动流程: GameLoop 初始化 → 角色选择界面自动确认 (P1=Ryu, P2=Ken) → 自动跳转训练场景
3. 观察 Output 面板:
   - 不应有红色错误信息
   - 不应有 `[CharacterController] P1/P2: xxx is not assigned` 警告 (如果没有第 1.3 步绑定, 这里会出现警告)
   - 应看到角色选择确认日志: `[CharacterSelect] P1 selected ...`, `[CharacterSelect] Match initialized: ...`
   - 应看到 Data 加载日志: `[Data] Loaded X moves.`, `[Data] Loaded X characters.`

> **检查点**: 无错误、无未分配警告, 自动跳转到训练场景 = PASS

### 2.4 验证双角色实例化 (AC-4, AC-8)

1. 游戏运行后, 查看 Game 窗口
2. 应看到两个角色实例:
   - P1 位于可见画面中心左侧
   - P2 位于可见画面中心右侧
   - 两者水平间距约 400 像素
3. 每个角色头顶应显示 StateDebugLabel, 文字内容为 `[P1] Idle` 和 `[P2] Idle`（颜色取决于当前主题）

> **检查点**: 两个角色分别显示 `[P1] Idle` 和 `[P2] Idle` = PASS

### 2.5 验证输入响应 & 状态切换 (AC-4)

在游戏运行时, 按下以下键盘按键并观察状态变化:

#### P1 移动按键 (WASD 风格)
| 按键 | 方向 | 预期效果 |
|------|------|----------|
| `D` | 前进 (右) | 角色面朝右, 可能进入 Walk 状态 |
| `A` | 后退 (左) | 角色面朝左 |
| `S` | 蹲下 | 可能进入 Crouch 状态 |
| `Space` | 跳跃 | 可能进入 JumpStartup 状态 |

#### P1 攻击按键
| 按键 | 攻击 | 预期效果 |
|------|------|----------|
| `U` | 按钮 A (轻攻击) | 当前方向为 Neutral 时启动 `5LP`，完成后返回 `[P1] Idle` |
| `I` | 按钮 B (重攻击) | 当前方向为 Neutral 时启动 `5HP`，完成后返回 `[P1] Idle` |
| `K` | 按钮 C | 仅在已输入对应方向序列时启动 `dp_c` 或 `fireball_c` |
| `J` | 按钮 D | 仅在已输入 Forward → Down → DownForward 时启动 `dp_d` |

> **注意**: 由于当前 GameLoop 只处理 P1 的输入 (P2 无按键绑定), P2 在移动按键下不会响应。这是已知限制, 见 Story 1.3 Dev Notes。

> **检查点（待复验）**: 分别从当前帧 Neutral 轻按一次 U/I；标签完整经过攻击阶段并返回 Idle。先输入方向再按 U/I 不得误触发普通技。

> **输入匹配跟进修正**: matcher 现选择窗口内最新完成的方向序列；连续多帧 Neutral 后，当前帧 Neutral+A/B 不再被旧 Neutral 的帧号误拒绝。此项仍须按上述步骤重新人工确认。

### 2.6 验证训练模式测试按键 (故事间集成测试)

训练场景 (`TrainingScene`) 提供了一组调试按键:

| 按键 | 功能 | 预期效果 |
|------|------|----------|
| `P` | 暂停/恢复 | 帧引擎暂停, 画面冻结 |
| `→` (右箭头) | 逐帧前进 | 暂停状态下前进一帧 |
| `←` (左箭头) | 逐帧后退 | 暂停状态下后退一帧 |
| `H` | 测试击中 | P2 进入 Hitstun，30 个实际处理帧后返回 Idle；暂停不计数 |
| `B` | 测试格挡 | P2 进入 Blockstun，20 个实际处理帧后返回 Idle；替换状态取消旧计时 |
| `O` | 切换 HitboxOverlay | 显示/隐藏判定框叠加层 |
| `1` | 切换 P1 输入日志 | 显示/隐藏 P1 的历史输入记录 |
| `2` | 切换 P2 输入日志 | 显示/隐藏 P2 的历史输入记录 |

**重点测试流程:**
1. 运行游戏, 确认角色显示 `[P1] Idle` 和 `[P2] Idle`
2. 当前方向为 Neutral 时轻按 `U`，确认 `5LP` 完成并返回 `[P1] Idle`
3. 当前方向为 Neutral 时轻按 `I`，确认 `5HP` 完成并返回 `[P1] Idle`
4. 按 `H`，确认 P2 显示 Hitstun；暂停期间不消耗计时，恢复后累计 30 个处理帧返回 Idle
5. 按 `B`，确认 P2 显示 Blockstun，并在 20 个处理帧后返回 Idle

> **检查点（待复验）**: H/B 状态、精确恢复时长、暂停冻结及最终 Idle 均符合预期。

### 2.7 验证双玩家独立性 (AC-4, AC-8)

1. 运行游戏
2. 按 `U` (P1 攻击) → P1 状态变化, P2 保持 Idle
3. 按 `H` (击中 P2) → P2 进入 Hitstun, P1 状态不受 P2 状态影响
4. 两个角色的 StateDebugLabel 应独立变化

> **检查点（待复验）**: P1/P2 独立完成各自生命周期并返回 Idle。

### 2.8 验证关闭流程 (AC-3)

1. 关闭游戏窗口 (或按 `Esc` / `Alt+F4`)
2. 观察 Output 面板: 不应有异常错误
3. 特别确认没有 "ObjectDisposedException" 或 "zombie subscription" 相关错误

> **检查点**: 关闭时无异常 = PASS

---

## 第三部分: 已知限制与注意事项

### 3.1 P2 输入未绑定
GameLoop._Process() 目前只处理 PlayerId=1 的键盘输入。P2 实例能正确显示初始状态和接收事件 (如 Hitstun), 但无法通过键盘直接控制。这是计划在 Story 1.5 (Physics) 或后续输入路由故事中解决的问题。

### 3.2 角色为空白精灵
模板中没有默认的精灵图 (AC-2 的意图)。在 Godot 中运行时角色会显示为不可见或空白 — 这是正常行为。如果需要在视觉上区分两个角色, 可以为 Sprite2D 节点临时分配一个颜色方块纹理。

替代方案: 通过 StateDebugLabel 的文本内容来确认角色状态, 不需要依赖视觉外观。

### 3.3 移动系统未实装
Walk/Crouch/Jump 等移动状态在 StateMachine 中有定义, CharacterViewModel 也有对应映射, 但实际的位置更新逻辑 (position-updating) 在 Story 1.5 才会实现。目前按方向键后状态标签可能变化, 但角色不会在屏幕上移动。

### 3.4 Hurtbox 未激活
Hurtbox Area2D 节点设置为 `Monitoring = false`, 目前不会参与碰撞检测。碰撞逻辑在 Story 1.5 中实现。但节点结构已经就位, `CharacterController.GetHurtboxes()` 可以正确返回所有 Hurtbox 引用。

### 3.5 AnimationPlayer 无动画
模板中的 AnimationPlayer 没有任何预置动画。调用 `AnimationPlayer.Play("idle")` 等方法时会静默无操作 (不会崩溃)。开发者需要自己添加以 `GetAnimationName()` 返回的名称命名的动画轨道:

| 状态 | 动画名称 |
|------|----------|
| Idle | `idle` |
| Walk | `walk` |
| Crouch | `crouch` |
| JumpStartup | `jump_startup` |
| JumpActive | `jump_active` |
| JumpRecovery | `jump_recovery` |
| AttackStartup | `attack_startup` |
| AttackActive | `attack_active` |
| AttackRecovery | `attack_recovery` |
| Hitstun | `hitstun` |
| Blockstun | `blockstun` |
| Knockdown | `knockdown` |
| Wakeup | `wakeup` |
| Airborne | `airborne` |

---

## 第四部分: 验证检查清单

按顺序逐项完成, 在 `[ ]` 中标记 `[x]`:

### Story 1.3 — Task 3.2 (Export 引用绑定)
- [x] 3.2.1 SpriteContainer 已绑定到 SpriteContainer 节点
- [x] 3.2.2 HurtboxContainer 已绑定到 HurtboxContainer 节点
- [x] 3.2.3 AnimationPlayer 已绑定到 AnimationPlayer 节点
- [x] 3.2.4 StateDebugLabel 已绑定到 StateDebugLabel 节点
- [x] 3.2.5 场景已保存 (Ctrl+S)

### Story 1.3 — Task 10 (Godot 编辑器验证)
- [x] 10.1 节点树结构与设计一致 (6 个顶层子节点)
- [x] 10.2 Inspector 中 6 个 Export 属性均可见
- [x] 10.3 F5 运行无错误/警告
- [x] 10.4 两个角色实例出现在训练场景中
- [x] 10.5 StateDebugLabel 显示 `[P1] Idle` 和 `[P2] Idle`
- [x] 10.6 U/I 分别启动 5LP/5HP，完成后返回 Idle（2026-07-31 已验收）
- [x] 10.7 H 使 P2 Hitstun 30 个处理帧，暂停冻结，随后 Idle（2026-07-31 已验收）
- [x] 10.8 B 使 P2 Blockstun 20 个处理帧，替换状态取消旧计时（2026-07-31 已验收）
- [x] 10.9 P 键暂停功能正常
- [x] 10.10 O 键切换 HitboxOverlay
- [x] 10.11 1/2 键切换输入日志显示
- [x] 10.12 双角色状态独立且分别返回 Idle（2026-07-31 已验收）
- [x] 10.13 关闭时无异常错误

---

## 第五部分: 故障排除

| 症状 | 可能原因 | 解决方案 |
|------|----------|----------|
| `[CharacterController] P1: SpriteContainer is not assigned` | Export 引用未绑定 | 回到第一部分步骤 1.3, 拖拽节点绑定 |
| `[CharacterController] P1: GameLoop autoload not found` | GameLoop 不在 autoload 列表中 | 检查 `project.godot` 的 autoload 配置 |
| 角色不显示 | Sprite 无纹理 | 正常行为—不是故障。参考 §3.2 |
| 按 U 键无反应 | 输入管道未处理 | 检查 Output 面板是否有异常; 检查 StateMachine 是否初始化 |
| `[CharacterController] P1: PlayerId must be 1 or 2` | PlayerId 设置错误 | 检查 Inspector 中 PlayerId 是否为 1 或 2 |
| F5 崩溃 | 可能是主场景路径问题 | 确保 `project.godot` 的 `run/main_scene` 使用 `res://` 路径而非 `uid://` 路径 |

---

> **签署**: 完成验证后请在检查清单上标记并注明日期。
> **完成记录**: 验证已通过，Story 1.3 已于 2026-07-31 更新为 `done`；无需再次执行状态迁移。
