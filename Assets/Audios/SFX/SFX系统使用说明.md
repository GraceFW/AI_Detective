# SFX 音效系统使用说明（技术策划版）

## 📋 系统概述

SFX 音效系统是一个统一的音效管理框架，用于管理游戏中所有非 BGM 的音效播放。系统采用 ScriptableObject 配置库，支持灵活的音效参数配置，并提供防炸策略（冷却、并发限制）确保音效播放的稳定性。

### 核心特性

- ✅ **统一管理**：所有 SFX 音效通过 SfxManager 单例统一管理
- ✅ **配置驱动**：使用 ScriptableObject 配置库，无需硬编码
- ✅ **防炸策略**：支持冷却时间和并发数量限制
- ✅ **灵活接入**：提供组件和代码两种接入方式
- ✅ **AudioMixer 集成**：支持通过 AudioMixer 统一控制音量

---

## 🎯 音效类型

系统目前支持 3 种音效类型：

| 音效 ID | 用途 | 循环 | 冷却 | 最大并发 |
|---------|------|------|------|----------|
| **TypewriterLoop** | 打字机循环音效（问讯/搜索界面） | ✅ | ❌ | 1-2 |
| **Confirm** | 游戏确认音效（按钮点击、翻页等） | ❌ | ✅ 0.05-0.1s | 8 |
| **NewClue** | 新线索提示音（线索收集时） | ❌ | ❌ | 2 |

---

## 🛠️ 配置步骤

### 步骤 1：创建 AudioMixer（如未创建）

1. 在 Project 窗口右键：`Create > Audio > Audio Mixer`
2. 命名为 `AudioMixer`，保存到 `Assets/Audios/` 目录
3. 打开 AudioMixer 窗口（`Window > Audio > Audio Mixer`）
4. 在 Mixer 中创建 SFX 组：
   - 右键 Master 组 > `Add child group`
   - 命名为 `SFX`
   - 确保 SFX 组连接到 Master

### 步骤 2：创建 SfxLibrary 配置库

1. 在 Project 窗口右键：`Create > Audio > Sfx Library`
2. 命名为 `SfxLibrary`，保存到 `Assets/Audios/` 目录
3. 在 Inspector 中配置 3 个音效条目：

#### TypewriterLoop（打字机循环音效）
```
id: TypewriterLoop
clip: 拖入 Assets/Audios/打字机.mp3
volume: 0.5（建议 0.4-0.7）
loop: ✓ 勾选
pitchMin: 0.98
pitchMax: 1.02
maxSimultaneous: 2
cooldown: 0
```

#### Confirm（游戏确认音效）
```
id: Confirm
clip: 拖入 Assets/Audios/游戏确认音效.mp3
volume: 0.9（建议 0.8-1.0）
loop: ✗ 不勾选
pitchMin: 1
pitchMax: 1
maxSimultaneous: 8
cooldown: 0.08（建议 0.05-0.1）
```

#### NewClue（新线索提示音）
```
id: NewClue
clip: 拖入 Assets/Audios/消息提示音.mp3
volume: 0.9（建议 0.8-1.0）
loop: ✗ 不勾选
pitchMin: 1
pitchMax: 1
maxSimultaneous: 2
cooldown: 0
```

### 步骤 3：在 Persistent 场景配置 SfxManager

1. 打开 `Assets/Scenes/Persistent.unity` 场景
2. 在 Hierarchy 中创建空 GameObject，命名为 `AudioSystem`
3. 选中 AudioSystem，在 Inspector 中添加组件：`SfxManager`
4. 配置 SfxManager：
   - **Library**: 拖入 `Assets/Audios/SfxLibrary.asset`
   - **Sfx Mixer Group**: 拖入 AudioMixer 的 SFX 组（在 AudioMixer 窗口中，点击 SFX 组，然后在 Inspector 中拖入）
   - **One Shot Pool Size**: 16（默认值，可根据需要调整）

---

## 📖 使用方法

### 方法 1：使用 PlaySfxOnClick 组件（推荐，用于按钮）

**适用场景**：UI 按钮点击音效

**操作步骤**：
1. 选中需要添加音效的 Button 对象
2. 在 Inspector 中点击 `Add Component`
3. 添加 `Play Sfx On Click` 组件
4. 在组件中设置：
   - **Sfx Id**: 选择音效类型（默认 Confirm）

**示例**：
- 提交案件按钮
- 对话选项按钮
- 对话翻页按钮（上一条/下一条）
- 左右切换大页面按钮

**注意事项**：
- 组件会自动绑定到 Button 的 onClick 事件
- 不会破坏原有的 onClick 逻辑，只是额外播放音效
- 如果按钮是动态生成的，需要在生成时通过代码添加组件

### 方法 2：代码调用（用于复杂逻辑）

#### 播放一次性音效
```csharp
// 播放确认音效
SfxManager.Instance.Play(SfxId.Confirm);

// 播放新线索提示音
SfxManager.Instance.Play(SfxId.NewClue);
```

#### 播放循环音效
```csharp
// 开始播放循环音效（使用 this 作为 ownerKey ，ownkey是这个循环音效“属于谁”的唯一标识）
SfxManager.Instance.PlayLoop(SfxId.TypewriterLoop, this);

// 停止循环音效
SfxManager.Instance.StopLoop(SfxId.TypewriterLoop, this);

// 停止该对象的所有循环音效
SfxManager.Instance.StopAllLoops(this);
```

**注意事项**：
- 循环音效需要手动管理，确保在适当时机停止
- 使用 `ownerKey` 可以区分不同的音效实例（例如多个打字机同时工作）

---

## 🎮 已接入的功能点

### 1. 打字机音效（TypewriterLoop）

**接入位置**：
- 问讯界面：`DialogueUI.cs` 的 `dialogueText` 上的 TypewriterEffect
- 搜索界面：`SearchPanelController.cs` 的 `resultText` 上的 TypewriterEffect

**配置方式**：
1. 在场景或 Prefab 中找到对应的文本对象
2. 选中该对象，在 Inspector 中找到 `TypewriterEffect` 组件
3. 勾选 `Enable Typewriter Sfx`

**行为**：
- 打字开始时自动播放循环音效
- 打字结束时自动停止
- 跳过显示时自动停止
- 对象禁用时自动停止（兜底）

**注意**：黑底滚代码（FadeManager）不使用 TypewriterEffect，保持默认 false。

### 2. 确认音效（Confirm）

**已接入的按钮**：
- ✅ 提交案件按钮（`SettlementPanelUI.cs`）
- ✅ 对话选项按钮（`DialogueUI.cs`，动态生成）
- ✅ 对话翻页按钮（`DialogueUI.cs`，上一条/下一条）
- ✅ 左右切换大页面按钮（`UIPanelSwitcher.cs`）

**行为**：
- 点击按钮时自动播放
- 有冷却时间（0.05-0.1s），防止连点爆音

### 3. 新线索提示音（NewClue）

**接入位置**：`ClueManager.cs` 的 `RevealClue()` 方法

**行为**：
- 仅在真正新增线索时播放（重复不播）
- 最大并发 2 个，防止同时收集多个线索时爆音

---

## 🔧 API 参考

### SfxManager 主要方法

#### Play(SfxId id)
播放一次性音效。

**参数**：
- `id`: 音效 ID（TypewriterLoop / Confirm / NewClue）

**示例**：
```csharp
SfxManager.Instance.Play(SfxId.Confirm);
```

#### PlayLoop(SfxId id, object ownerKey)
播放循环音效。

**参数**：
- `id`: 音效 ID（目前仅 TypewriterLoop 支持循环）
- `ownerKey`: 拥有者标识（用于管理多个循环音效实例）

**示例**：
```csharp
SfxManager.Instance.PlayLoop(SfxId.TypewriterLoop, this);
```

#### StopLoop(SfxId id, object ownerKey)
停止指定的循环音效。

**参数**：
- `id`: 音效 ID
- `ownerKey`: 拥有者标识

**示例**：
```csharp
SfxManager.Instance.StopLoop(SfxId.TypewriterLoop, this);
```

#### StopAllLoops(object ownerKey)
停止指定拥有者的所有循环音效。

**参数**：
- `ownerKey`: 拥有者标识

**示例**：
```csharp
SfxManager.Instance.StopAllLoops(this);
```

---

## ⚙️ 配置参数说明

### SfxEntry 配置项

| 参数 | 说明 | 取值范围 | 建议值 |
|------|------|----------|--------|
| **id** | 音效 ID | TypewriterLoop / Confirm / NewClue | - |
| **clip** | 音频片段 | AudioClip 资源 | - |
| **volume** | 音量 | 0.0 - 1.0 | 0.4-1.0 |
| **loop** | 是否循环 | true / false | TypewriterLoop: true<br>其他: false |
| **pitchMin** | 音调最小值 | 0.5 - 2.0 | 0.98-1.02 |
| **pitchMax** | 音调最大值 | 0.5 - 2.0 | 0.98-1.02 |
| **maxSimultaneous** | 最大同时播放数 | 1 - 16 | 1-8 |
| **cooldown** | 冷却时间（秒） | 0.0 - 1.0 | 0.05-0.1 |

### 防炸策略说明

1. **冷却时间（cooldown）**
   - 在冷却时间内，相同音效的播放请求会被拒绝
   - 适用于 Confirm 音效，防止连点爆音

2. **并发限制（maxSimultaneous）**
   - 超过最大并发数时，新的播放请求会被拒绝
   - 适用于 NewClue 音效，防止同时收集多个线索时爆音

3. **循环音效管理**
   - 同一 ownerKey 只允许一个循环音效
   - 适用于 TypewriterLoop，确保同一打字机只有一个循环音效

---

## 🎨 添加新音效的流程

### 1. 添加音效 ID
在 `Assets/Scripts/Audio/SfxId.cs` 中添加新的枚举值：
```csharp
public enum SfxId
{
    TypewriterLoop,
    Confirm,
    NewClue,
    YourNewSfx  // 新增
}
```

### 2. 在 SfxLibrary.asset 中配置
1. 选中 `Assets/Audios/SfxLibrary.asset`
2. 在 Inspector 中点击 `+` 添加新条目
3. 配置参数（参考上表）

### 3. 在代码中调用
```csharp
SfxManager.Instance.Play(SfxId.YourNewSfx);
```

---

## ❓ 常见问题

### Q1: 音效没有播放？
**检查清单**：
1. ✅ SfxManager 是否在 Persistent 场景中正确配置？
2. ✅ SfxLibrary.asset 是否绑定到 SfxManager？
3. ✅ AudioMixer 的 SFX 组是否绑定？
4. ✅ 音效配置中的 clip 是否已赋值？
5. ✅ 是否在冷却时间内（Confirm 音效）？
6. ✅ 是否超过最大并发数？

### Q2: 打字机音效没有播放？
**检查清单**：
1. ✅ TypewriterEffect 组件的 `Enable Typewriter Sfx` 是否勾选？
2. ✅ 是否在问讯界面或搜索界面（黑底滚代码不启用）？
3. ✅ SfxLibrary 中 TypewriterLoop 的配置是否正确？

### Q3: 如何调整音效音量？
**方法 1**：在 SfxLibrary.asset 中调整单个音效的 volume 参数
**方法 2**：在 AudioMixer 窗口中调整 SFX 组的音量（影响所有 SFX）

### Q4: 如何临时禁用某个音效？
在 SfxLibrary.asset 中，将该音效的 clip 设置为 None，或删除该条目。

### Q5: 动态生成的按钮如何添加音效？
在生成按钮的代码中，添加 PlaySfxOnClick 组件：
```csharp
var button = buttonObj.GetComponent<Button>();
if (button != null)
{
    var playSfx = buttonObj.GetComponent<PlaySfxOnClick>();
    if (playSfx == null)
    {
        playSfx = buttonObj.AddComponent<PlaySfxOnClick>();
    }
}
```

---

## 📝 注意事项

1. **单例模式**：SfxManager 使用单例模式，在 Persistent 场景中常驻（DontDestroyOnLoad）
2. **配置顺序**：必须先创建 AudioMixer 和 SfxLibrary.asset，再配置 SfxManager
3. **循环音效管理**：循环音效需要手动停止，确保在适当时机调用 StopLoop
4. **防炸策略**：不要随意提高 maxSimultaneous 和降低 cooldown，可能导致音效爆音
5. **AudioMixer 集成**：所有音效都输出到 SFX 组，可以通过 AudioMixer 统一控制音量

---

## 📚 相关文件

### 核心脚本
- `Assets/Scripts/Audio/SfxId.cs` - 音效 ID 枚举
- `Assets/Scripts/Audio/SfxEntry.cs` - 音效配置数据类
- `Assets/Scripts/Audio/SfxLibrary.cs` - ScriptableObject 配置库
- `Assets/Scripts/Audio/SfxManager.cs` - 单例管理器
- `Assets/Scripts/Audio/PlaySfxOnClick.cs` - 按钮音效组件

### 配置文件
- `Assets/Audios/SfxLibrary.asset` - 音效配置库（需在 Unity Editor 中创建）
- `Assets/Audios/AudioMixer.mixer` - 音频混合器（需在 Unity Editor 中创建）

### 接入点
- `Assets/Scripts/UI/TypewriterEffect.cs` - 打字机音效接入
- `Assets/Scripts/Clues/ClueManager.cs` - 新线索提示音接入
- `Assets/Scripts/UI/ASKDialogue/DialogueUI.cs` - 对话按钮音效
- `Assets/Scripts/UI/UIPanelSwitcher.cs` - 页面切换按钮音效
- `Assets/Scripts/UI/SettlementPanelUI.cs` - 提交按钮音效

---

## 🎯 快速检查清单

配置完成后，请检查：
- [ ] AudioMixer 已创建，包含 Master 和 SFX 组
- [ ] SfxLibrary.asset 已创建，配置了 3 个音效条目
- [ ] Persistent 场景中有 AudioSystem GameObject
- [ ] AudioSystem 上挂载了 SfxManager 组件
- [ ] SfxManager 的 Library 和 Sfx Mixer Group 已绑定
- [ ] 问讯界面和搜索界面的 TypewriterEffect 已开启 `Enable Typewriter Sfx`
- [ ] 所有确认按钮已添加 PlaySfxOnClick 组件
- [ ] 运行游戏测试所有音效是否正常播放

---

**文档版本**：v1.0  
**最后更新**：2024  
**维护者**：技术团队

