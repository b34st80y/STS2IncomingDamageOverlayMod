using System.Collections;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2IncomingDamageOverlayMod.Application;
using STS2IncomingDamageOverlayMod.Domain;
using static STS2IncomingDamageOverlayMod.Infrastructure.GodotSceneWalker;
using static STS2IncomingDamageOverlayMod.Infrastructure.ReflectionMemberReader;

namespace STS2IncomingDamageOverlayMod.Infrastructure;

internal sealed class CombatDamageReader : ICombatDamageReader
{
    private Node? _combatNode;

    public IncomingDamageSnapshot Read(Node sceneRoot)
    {
        _combatNode ??= FindCombatNode(sceneRoot);

        if (_combatNode is null || !GodotObject.IsInstanceValid(_combatNode))
        {
            _combatNode = null;
            return new IncomingDamageSnapshot(0, 0, 0, false, false, Colors.White);
        }

        object? combatState = ReadMember(_combatNode, "CombatState") ?? ReadMember(_combatNode, "_combatState");
        string localPlayerId = FindLocalPlayerId(combatState, _combatNode);
        PlayerCreatureInfo? localPlayer = FindPlayerCreature(sceneRoot, localPlayerId);
        PlayerVitals playerVitals = GetPlayerVitals(localPlayer);
        IntentDamage typedDamage = SumTypedCreatureIntentDamage(sceneRoot, localPlayer);

        int incoming = typedDamage.Total;
        if (!typedDamage.HasTypedIntents)
        {
            incoming = SumIncomingDamage(combatState);
            if (incoming <= 0)
            {
                incoming = SumVisibleIntentDamage(sceneRoot);
            }
        }

        if (incoming <= 0)
        {
            return new IncomingDamageSnapshot(0, 0, 0, false, playerVitals.HasDefensivePotion, playerVitals.CharacterColor);
        }

        int afterBlock = Math.Max(0, incoming - playerVitals.Block);
        int afterOsty = Math.Max(0, afterBlock - playerVitals.OstyHp);
        bool isLethal = playerVitals.CurrentHp > 0 && afterOsty >= playerVitals.CurrentHp;
        return new IncomingDamageSnapshot(
            incoming,
            afterBlock,
            afterOsty,
            isLethal,
            playerVitals.HasDefensivePotion,
            playerVitals.CharacterColor);
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

    private static string FindLocalPlayerId(object? combatState, Node? combatNode)
    {
        string id = ReadString(
            combatState,
            "LocalPlayerId",
            "LocalPlayerID",
            "_localPlayerId",
            "GetLocalPlayerId");
        return id.Length > 0
            ? id
            : ReadString(combatNode, "LocalPlayerId", "LocalPlayerID", "_localPlayerId", "GetLocalPlayerId");
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

    private static IntentDamage SumTypedCreatureIntentDamage(Node root, PlayerCreatureInfo? localCreature)
    {
        int total = 0;
        bool hasTypedIntents = false;

        foreach (NCreature creatureNode in Walk(root).OfType<NCreature>())
        {
            Creature? owner = creatureNode.Entity;
            if (owner is null || !owner.IsMonster || !owner.IsEnemy || owner.IsDead)
            {
                continue;
            }

            IReadOnlyList<Creature> targets = localCreature is not null
                ? [localCreature.Creature]
                : owner.CombatState?.PlayerCreatures ?? Array.Empty<Creature>();
            object? nextMove = owner.Monster?.NextMove;
            if (nextMove is null)
            {
                continue;
            }

            IReadOnlyList<AbstractIntent> intents = owner.Monster?.NextMove?.Intents ?? Array.Empty<AbstractIntent>();
            hasTypedIntents = true;
            foreach (AbstractIntent intent in intents)
            {
                if (intent is AttackIntent attackIntent)
                {
                    total += attackIntent.GetTotalDamage(targets, owner);
                }
            }
        }

        return new IntentDamage(total, hasTypedIntents);
    }

    private static PlayerVitals GetPlayerVitals(PlayerCreatureInfo? player)
    {
        if (player is null)
        {
            return new PlayerVitals(0, 0, 0, false, Colors.White);
        }

        Creature entity = player.Creature;
        bool hasDefensivePotion = entity.Player?.Potions.Any(IsDefensiveOrWeakPotion) == true;
        string characterName = FindCharacterName(entity, player.Node);
        int ostyHp = characterName.Contains("Necrobinder", StringComparison.OrdinalIgnoreCase)
            ? FindOstyHp(player.Node.GetTree().Root)
            : 0;
        return new PlayerVitals(entity.Block, entity.CurrentHp, ostyHp, hasDefensivePotion, GetCharacterColor(characterName));
    }

    private static int FindOstyHp(Node root)
    {
        foreach (NCreature creatureNode in Walk(root).OfType<NCreature>())
        {
            Creature? creature = creatureNode.Entity;
            if (creature is null || creature.IsDead || !LooksLikeOsty(creatureNode, creature))
            {
                continue;
            }

            return Math.Max(0, creature.CurrentHp);
        }

        return 0;
    }

    private static bool LooksLikeOsty(NCreature creatureNode, Creature creature)
    {
        object?[] candidates =
        [
            creatureNode,
            creature,
            creature.Monster,
            creature.Player,
            ReadMember(creature, "Model"),
            ReadMember(creature, "Definition"),
            ReadMember(creature, "CreatureModel"),
            ReadMember(creature, "CreatureDefinition")
        ];

        foreach (object? candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            string text = $"{ReadString(candidate, "Id", "ID", "Name", "Key")} {candidate.GetType().Name} {candidate.GetType().FullName}";
            if (text.Contains("Osty", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (Node node in Walk(creatureNode))
        {
            string text = $"{node.Name} {node.GetType().Name} {node.GetType().FullName}";
            if (text.Contains("Osty", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record PlayerCreatureInfo(NCreature Node, Creature Creature);

    private static PlayerCreatureInfo? FindPlayerCreature(Node root, string localPlayerId)
    {
        List<PlayerCreatureInfo> players = Walk(root)
            .OfType<NCreature>()
            .Select(creatureNode => new PlayerCreatureInfo(creatureNode, creatureNode.Entity!))
            .Where(player => player.Creature is not null && player.Creature.IsPlayer && !player.Creature.IsDead)
            .ToList();

        PlayerCreatureInfo? localFlagMatch = players.FirstOrDefault(IsLocalPlayerCreature);
        if (localFlagMatch is not null)
        {
            return localFlagMatch;
        }

        if (localPlayerId.Length > 0)
        {
            PlayerCreatureInfo? idMatch = players.FirstOrDefault(
                player => string.Equals(
                    FindPlayerId(player.Creature, player.Creature.Player, player.Node),
                    localPlayerId,
                    StringComparison.OrdinalIgnoreCase));
            if (idMatch is not null)
            {
                return idMatch;
            }
        }

        return players.Count == 1 ? players[0] : null;
    }

    private static bool IsLocalPlayerCreature(PlayerCreatureInfo player)
    {
        if (IsLocalObject(player.Node) || IsLocalObject(player.Creature) || IsLocalObject(player.Creature.Player))
        {
            return true;
        }

        return Walk(player.Node).Any(IsLocalObject);
    }

    private static string FindPlayerId(params object?[] candidates)
    {
        foreach (object? candidate in candidates)
        {
            string id = ReadString(
                candidate,
                "LocalPlayerId",
                "LocalPlayerID",
                "_localPlayerId",
                "GetLocalPlayerId",
                "PlayerId",
                "PlayerID",
                "_playerId",
                "PeerId",
                "PeerID",
                "_peerId",
                "NetId",
                "NetID",
                "_netId",
                "UserId",
                "UserID",
                "_userId",
                "OwnerId",
                "OwnerID",
                "_ownerId",
                "Id",
                "ID");
            if (id.Length > 0)
            {
                return id;
            }
        }

        return "";
    }

    private static bool IsLocalObject(object? source)
    {
        return ReadBool(source, "IsLocal", "_isLocal", "Local", "IsLocalPlayer", "_displayLocalPlayer");
    }

    private static Color GetCharacterColor(string characterName)
    {
        if (characterName.Contains("Ironclad", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(1f, 0.12f, 0.08f);
        }

        if (characterName.Contains("Silent", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.24f, 0.9f, 0.32f);
        }

        if (characterName.Contains("Regeant", StringComparison.OrdinalIgnoreCase) ||
            characterName.Contains("Regent", StringComparison.OrdinalIgnoreCase))
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

    private static string FindCharacterName(Creature player, NCreature? playerNode)
    {
        string nodeName = FindCharacterNameFromNode(playerNode);
        if (nodeName.Length > 0)
        {
            return nodeName;
        }

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

    private static string FindCharacterNameFromNode(Node? playerNode)
    {
        if (playerNode is null)
        {
            return "";
        }

        foreach (Node node in Walk(playerNode))
        {
            string text = $"{node.Name} {node.GetType().Name} {node.GetType().FullName}";
            if (ContainsKnownCharacterName(text))
            {
                return text;
            }
        }

        return "";
    }

    private static bool ContainsKnownCharacterName(string text)
    {
        return text.Contains("Ironclad", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Silent", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Regeant", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Regent", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Necrobinder", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Defect", StringComparison.OrdinalIgnoreCase);
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
}
