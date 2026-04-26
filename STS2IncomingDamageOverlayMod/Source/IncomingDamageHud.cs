using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace STS2IncomingDamageOverlayMod;

public partial class IncomingDamageHud : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly Label _label = new();
    private readonly OverlayConfig _config = OverlayConfig.Load();
    private bool _isDragging;
    private bool _wasEditMode;
    private Vector2 _dragOffset;
    private double _nextScanAt;
    private Node? _combatNode;

    public override void _Ready()
    {
        Layer = 128;

        _panel.Name = "STS2IncomingDamageOverlayModPanel";
        _panel.Position = _config.Position;
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle(new Color(0, 0, 0, 0)));

        _label.Name = "STS2IncomingDamageOverlayModLabel";
        _label.Size = new Vector2(520, 60);
        _label.Text = "";
        _label.Visible = false;
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;
        _label.HorizontalAlignment = HorizontalAlignment.Left;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _label.AddThemeConstantOverride("shadow_offset_x", 2);
        _label.AddThemeConstantOverride("shadow_offset_y", 2);

        _panel.AddChild(_label);
        AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        UpdateEditMode();

        if (Time.GetTicksMsec() / 1000.0 >= _nextScanAt)
        {
            _nextScanAt = Time.GetTicksMsec() / 1000.0 + 0.20;
            UpdateIncomingDamage();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsEditMode())
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouseButton)
        {
            if (mouseButton.Pressed && IsMouseInsidePanel(mouseButton.Position))
            {
                _isDragging = true;
                _dragOffset = mouseButton.Position - _panel.Position;
                GetViewport().SetInputAsHandled();
            }
            else if (!mouseButton.Pressed && _isDragging)
            {
                _isDragging = false;
                SavePosition();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion && _isDragging)
        {
            _panel.Position = ClampToViewport(mouseMotion.Position - _dragOffset);
            GetViewport().SetInputAsHandled();
        }
    }

    private void UpdateIncomingDamage()
    {
        _combatNode ??= FindCombatNode(GetTree().Root);

        if (_combatNode is null || !IsInstanceValid(_combatNode))
        {
            _combatNode = null;
            _panel.Visible = false;
            _label.Visible = false;
            return;
        }

        object? combatState = ReadMember(_combatNode, "CombatState") ?? ReadMember(_combatNode, "_combatState");
        object? localPlayer = FindLocalPlayer(combatState);
        PlayerVitals playerVitals = FindPlayerVitals(GetTree().Root, localPlayer);
        int block = playerVitals.Block;
        if (block <= 0)
        {
            block = ReadInt(localPlayer, "Block", "CurrentBlock", "block");
        }
        int incoming = SumTypedCreatureIntentDamage(GetTree().Root, localPlayer);
        if (incoming <= 0)
        {
            incoming = SumIncomingDamage(combatState);
        }
        if (incoming <= 0)
        {
            incoming = SumVisibleIntentDamage(GetTree().Root);
        }

        if (incoming <= 0)
        {
            _panel.Visible = false;
            _label.Visible = false;
            return;
        }

        int afterBlock = Math.Max(0, incoming - block);
        bool isLethal = playerVitals.CurrentHp > 0 && afterBlock >= playerVitals.CurrentHp;
        string reminder = afterBlock > 0 && playerVitals.HasPotion
            ? "    Defensive Potion Available"
            : "";
        _panel.Visible = true;
        _label.Visible = true;
        _label.Text = isLethal
            ? $"Incoming: {incoming}    After block: {afterBlock}{reminder}    LETHAL"
            : $"Incoming: {incoming}    After block: {afterBlock}{reminder}";
        _label.AddThemeColorOverride("font_color", playerVitals.CharacterColor);
    }

    private void UpdateEditMode()
    {
        bool editMode = IsEditMode();
        if (editMode == _wasEditMode)
        {
            return;
        }

        _wasEditMode = editMode;
        _panel.MouseFilter = editMode ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        _label.MouseFilter = editMode ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
        _panel.AddThemeStyleboxOverride("panel", MakePanelStyle(editMode ? new Color(0, 0, 0, 0.45f) : new Color(0, 0, 0, 0)));

        if (!editMode)
        {
            _isDragging = false;
            SavePosition();
        }
    }

    private static bool IsEditMode()
    {
        return Input.IsKeyPressed(Key.Ctrl);
    }

    private bool IsMouseInsidePanel(Vector2 mousePosition)
    {
        Rect2 rect = new(_panel.GlobalPosition, _panel.Size);
        return rect.HasPoint(mousePosition);
    }

    private Vector2 ClampToViewport(Vector2 position)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 max = new(Math.Max(0, viewportSize.X - _panel.Size.X), Math.Max(0, viewportSize.Y - _panel.Size.Y));
        return new Vector2(Math.Clamp(position.X, 0, max.X), Math.Clamp(position.Y, 0, max.Y));
    }

    private void SavePosition()
    {
        _config.SetPosition(_panel.Position);
        _config.Save();
    }

    private static StyleBoxFlat MakePanelStyle(Color background)
    {
        StyleBoxFlat style = new()
        {
            BgColor = background,
            BorderColor = new Color(1, 1, 1, background.A > 0 ? 0.55f : 0),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ContentMarginLeft = 8,
            ContentMarginTop = 4,
            ContentMarginRight = 8,
            ContentMarginBottom = 4
        };
        return style;
    }

    private static Node? FindCombatNode(Node root)
    {
        if (root.GetType().FullName?.Contains(".Combat.") == true &&
            (ReadMember(root, "CombatState") is not null || ReadMember(root, "_combatState") is not null))
        {
            return root;
        }

        foreach (Node child in root.GetChildren())
        {
            Node? found = FindCombatNode(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static object? FindLocalPlayer(object? combatState)
    {
        return ReadMember(combatState, "LocalPlayer")
            ?? ReadMember(combatState, "Player")
            ?? FirstItem(ReadMember(combatState, "Players"))
            ?? FirstItem(ReadMember(combatState, "PlayerCreatures"));
    }

    private static int SumIncomingDamage(object? combatState)
    {
        int total = 0;

        foreach (object creature in Enumerate(ReadMember(combatState, "Monsters"))
                     .Concat(Enumerate(ReadMember(combatState, "AllMonsters")))
                     .Concat(Enumerate(ReadMember(combatState, "Creatures"))))
        {
            if (LooksLikePlayer(creature))
            {
                continue;
            }

            total += SumDamageFromObject(ReadMember(creature, "Intent") ?? ReadMember(creature, "CurrentIntent"));
            total += SumDamageFromObject(ReadMember(creature, "Intents"));
        }

        total += SumDamageFromObject(ReadMember(combatState, "Intents"));
        return total;
    }

    private static int SumTypedCreatureIntentDamage(Node root, object? localPlayer)
    {
        int total = 0;
        Creature? localCreature = FindPlayerCreature(root, localPlayer);

        foreach (NCreature creatureNode in Walk(root).OfType<NCreature>())
        {
            Creature? owner = creatureNode.Entity;
            if (owner is null || !owner.IsMonster || !owner.IsEnemy || owner.IsDead)
            {
                continue;
            }

            IReadOnlyList<Creature> targets = localCreature is not null
                ? [localCreature]
                : owner.CombatState?.PlayerCreatures ?? Array.Empty<Creature>();
            foreach (AbstractIntent intent in owner.Monster?.NextMove?.Intents ?? Array.Empty<AbstractIntent>())
            {
                if (intent is AttackIntent attackIntent)
                {
                    total += attackIntent.GetTotalDamage(targets, owner);
                }
            }
        }

        return total;
    }

    private static PlayerVitals FindPlayerVitals(Node root, object? localPlayer)
    {
        Creature? entity = FindPlayerCreature(root, localPlayer);
        if (entity is null)
        {
            return new PlayerVitals(0, 0, false, Colors.White);
        }

        bool hasDefensivePotion = entity.Player?.Potions.Any(IsDefensiveOrWeakPotion) == true;
        return new PlayerVitals(
            entity.Block,
            entity.CurrentHp,
            hasDefensivePotion,
            GetCharacterColor(entity));
    }

    private readonly record struct PlayerVitals(int Block, int CurrentHp, bool HasPotion, Color CharacterColor);

    private static Creature? FindPlayerCreature(Node root, object? localPlayer)
    {
        List<Creature> players = Walk(root)
            .OfType<NCreature>()
            .Select(creatureNode => creatureNode.Entity)
            .Where(entity => entity is not null && entity.IsPlayer && !entity.IsDead)
            .Cast<Creature>()
            .ToList();

        if (localPlayer is not null)
        {
            Creature? matched = players.FirstOrDefault(player => IsSamePlayer(player, localPlayer));
            if (matched is not null)
            {
                return matched;
            }
        }

        return players.FirstOrDefault();
    }

    private static bool IsSamePlayer(Creature playerCreature, object localPlayer)
    {
        if (ReferenceEquals(playerCreature, localPlayer) || ReferenceEquals(playerCreature.Player, localPlayer))
        {
            return true;
        }

        object? localCreature = ReadMember(localPlayer, "Creature")
                                ?? ReadMember(localPlayer, "PlayerCreature")
                                ?? ReadMember(localPlayer, "Entity");
        if (ReferenceEquals(playerCreature, localCreature) || ReferenceEquals(playerCreature.Player, localCreature))
        {
            return true;
        }

        object? candidatePlayer = ReadMember(localPlayer, "Player");
        if (ReferenceEquals(playerCreature.Player, candidatePlayer))
        {
            return true;
        }

        string playerId = FindPlayerId(playerCreature, playerCreature.Player);
        string localId = FindPlayerId(localPlayer, candidatePlayer, localCreature);
        return playerId.Length > 0 &&
               localId.Length > 0 &&
               string.Equals(playerId, localId, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindPlayerId(params object?[] candidates)
    {
        foreach (object? candidate in candidates)
        {
            string id = ReadString(
                candidate,
                "PlayerId",
                "PlayerID",
                "PeerId",
                "PeerID",
                "NetId",
                "NetID",
                "UserId",
                "UserID",
                "Id",
                "ID");
            if (id.Length > 0)
            {
                return id;
            }
        }

        return "";
    }

    private static Color GetCharacterColor(Creature player)
    {
        string characterName = FindCharacterName(player);
        if (characterName.Contains("Ironclad", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(1f, 0.12f, 0.08f);
        }

        if (characterName.Contains("Silent", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.24f, 0.9f, 0.32f);
        }

        if (characterName.Contains("Regeant", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(1f, 0.52f, 0.08f);
        }

        if (characterName.Contains("Necrobinder", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.68f, 0.36f, 1f);
        }

        if (characterName.Contains("Defect", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.2f, 0.62f, 1f);
        }

        return Colors.White;
    }

    private static string FindCharacterName(Creature player)
    {
        object? playerModel = player.Player;
        object?[] candidates =
        [
            ReadMember(playerModel, "Character"),
            ReadMember(playerModel, "CharacterType"),
            ReadMember(playerModel, "PlayerClass"),
            ReadMember(playerModel, "Definition"),
            ReadMember(playerModel, "Model"),
            playerModel,
            player
        ];

        foreach (object? candidate in candidates)
        {
            string name = ReadString(candidate, "Id", "ID", "Name", "CharacterId", "CharacterID", "Key");
            if (name.Length > 0)
            {
                return name;
            }
        }

        foreach (object? candidate in candidates)
        {
            if (candidate is not null)
            {
                string typeName = candidate.GetType().FullName ?? candidate.GetType().Name;
                if (typeName.Length > 0)
                {
                    return typeName;
                }
            }
        }

        return "";
    }

    private static bool IsDefensiveOrWeakPotion(PotionModel potion)
    {
        string typeName = potion.GetType().Name;
        string[] defensivePotionNames =
        [
            "BlockPotion",
            "DexterityPotion",
            "Fortifier",
            "GhostInAJar",
            "LiquidBronze",
            "ShacklingPotion",
            "SkillPotion",
            "SpeedPotion",
            "WeakPotion"
        ];

        return defensivePotionNames.Contains(typeName, StringComparer.OrdinalIgnoreCase);
    }

    private static int SumDamageFromObject(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        int total = 0;
        foreach (object item in Enumerate(value).DefaultIfEmpty(value))
        {
            if (!LooksLikeAttackIntent(item))
            {
                continue;
            }

            int damage = ReadInt(item, "TotalDamage", "CalculatedDamage", "Damage", "BaseDamage", "CurrentDamage");
            int hits = Math.Max(1, ReadInt(item, "Hits", "Times", "NumHits", "HitCount", "AttackCount"));

            object? damageObject = ReadMember(item, "Damage");
            int nestedTotal = ReadInt(damageObject, "TotalDamage", "CalculatedDamage", "BaseDamage", "CurrentDamage");
            if (nestedTotal > damage)
            {
                damage = nestedTotal;
            }

            total += damage * hits;
        }

        return total;
    }

    private static int SumVisibleIntentDamage(Node root)
    {
        int total = 0;
        foreach (Node node in Walk(root))
        {
            string nodeName = node.Name.ToString();
            string typeName = node.GetType().FullName ?? node.GetType().Name;
            if (!nodeName.Contains("Intent", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Intent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Label label in Walk(node).OfType<Label>())
            {
                if (label.Name == "STS2IncomingDamageOverlayModLabel")
                {
                    continue;
                }

                total += ParseDamageText(label.Text);
            }
        }

        return total;
    }

    private static int ParseDamageText(string text)
    {
        string compact = text.Trim().ToLowerInvariant().Replace(" ", "");
        if (compact.Length == 0)
        {
            return 0;
        }

        string[] parts = compact.Split('x', '*');
        if (parts.Length == 2 &&
            int.TryParse(OnlyDigits(parts[0]), out int damage) &&
            int.TryParse(OnlyDigits(parts[1]), out int hits))
        {
            return damage * Math.Max(1, hits);
        }

        return int.TryParse(OnlyDigits(compact), out int single) ? single : 0;
    }

    private static string OnlyDigits(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static IEnumerable<Node> Walk(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren())
        {
            foreach (Node descendant in Walk(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool LooksLikeAttackIntent(object value)
    {
        string name = value.GetType().Name;
        string? fullName = value.GetType().FullName;
        string intentType = Convert.ToString(ReadMember(value, "IntentType")) ?? "";
        return name.Contains("Attack", StringComparison.OrdinalIgnoreCase)
               || fullName?.Contains("Attack", StringComparison.OrdinalIgnoreCase) == true
               || intentType.Contains("Attack", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePlayer(object value)
    {
        string? fullName = value.GetType().FullName;
        return fullName?.Contains(".Players.", StringComparison.OrdinalIgnoreCase) == true
               || value.GetType().Name.Contains("Player", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<object> Enumerate(object? value)
    {
        if (value is null || value is string)
        {
            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (object? item in dictionary.Values)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                if (item is not null)
                {
                    yield return item;
                }
            }
        }
    }

    private static object? FirstItem(object? value)
    {
        return Enumerate(value).FirstOrDefault();
    }

    private static int ReadInt(object? source, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = ReadMember(source, name);
            if (value is null)
            {
                continue;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                // Ignore non-numeric candidate members.
            }
        }

        return 0;
    }

    private static string ReadString(object? source, params string[] names)
    {
        foreach (string name in names)
        {
            object? value = ReadMember(source, name);
            if (value is null)
            {
                continue;
            }

            string? text = Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return "";
    }

    private static object? ReadMember(object? source, string name)
    {
        if (source is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = source.GetType();

        PropertyInfo? property = type.GetProperty(name, flags);
        if (property is not null && property.GetIndexParameters().Length == 0)
        {
            return SafeGet(() => property.GetValue(source));
        }

        FieldInfo? field = type.GetField(name, flags);
        if (field is not null)
        {
            return SafeGet(() => field.GetValue(source));
        }

        MethodInfo? method = type.GetMethods(flags)
            .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == 0);
        if (method is not null)
        {
            return SafeGet(() => method.Invoke(source, null));
        }

        return null;
    }

    private static object? SafeGet(Func<object?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }
}
