using NUnit.Framework;
using UnityEngine;

public sealed class CombatAiRelationshipPersonalityTests
{
    [Test]
    public void 下世話は相互に恋人の二人を選び近くで能力が上がる()
    {
        GameObject ownerObject = new GameObject("Gossiper");
        GameObject firstObject = new GameObject("FirstLover");
        GameObject secondObject = new GameObject("SecondLover");
        CharacterData firstData = null;
        CharacterData secondData = null;
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character first = CreateCharacter(firstObject, CombatTeam.Ally, new Vector3(-2f, 0f, 0f));
            Character second = CreateCharacter(secondObject, CombatTeam.Enemy, new Vector3(2f, 0f, 0f));
            firstData = CreateCharacterData(CharacterGender.Male);
            secondData = CreateCharacterData(CharacterGender.Female);
            SetLovers(firstData, secondData);
            SetCharacterData(first, firstData);
            SetCharacterData(second, secondData);
            profile = SetPersonality(owner, CombatAiPersonalityKind.Gossiper);
            CombatAiPersonalityRuntime runtime = ownerObject.AddComponent<CombatAiPersonalityRuntime>();

            runtime.Refresh();

            Assert.That(runtime.GossipFirst == first || runtime.GossipSecond == first, Is.True);
            Assert.That(runtime.GossipFirst == second || runtime.GossipSecond == second, Is.True);
            Assert.That(owner.STRBuff, Is.EqualTo(1.25f).Within(0.001f));
            Assert.That(owner.INTBuff, Is.EqualTo(1.25f).Within(0.001f));
        }
        finally
        {
            Destroy(profile, firstData, secondData, secondObject, firstObject, ownerObject);
        }
    }

    [Test]
    public void 下世話は見ている恋人が離脱すると次の戦闘まで復帰しない()
    {
        GameObject ownerObject = new GameObject("Gossiper");
        GameObject firstObject = new GameObject("FirstLover");
        GameObject secondObject = new GameObject("SecondLover");
        CharacterData firstData = null;
        CharacterData secondData = null;
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character first = CreateCharacter(firstObject, CombatTeam.Ally, new Vector3(-2f, 0f, 0f));
            Character second = CreateCharacter(secondObject, CombatTeam.Enemy, new Vector3(2f, 0f, 0f));
            firstData = CreateCharacterData(CharacterGender.Male);
            secondData = CreateCharacterData(CharacterGender.Female);
            SetLovers(firstData, secondData);
            SetCharacterData(first, firstData);
            SetCharacterData(second, secondData);
            profile = SetPersonality(owner, CombatAiPersonalityKind.Gossiper);
            CombatAiPersonalityRuntime runtime = ownerObject.AddComponent<CombatAiPersonalityRuntime>();
            runtime.Refresh();

            first.Health.TakeDamage(first.Health.MaxHP, second);
            runtime.Refresh();

            Assert.That(owner.Health.IsWithdrawn, Is.True);
            owner.Health.RestoreFull();
            Assert.That(owner.Health.IsWithdrawn, Is.False);
            Assert.That(owner.Health.IsAlive, Is.True);
        }
        finally
        {
            Destroy(profile, firstData, secondData, secondObject, firstObject, ownerObject);
        }
    }

    [Test]
    public void スケベは敵より同陣営の異性を選び距離で能力補正が変わる()
    {
        GameObject ownerObject = new GameObject("Lecherous");
        GameObject allyObject = new GameObject("AllyWoman");
        GameObject enemyObject = new GameObject("EnemyWoman");
        CharacterData ownerData = null;
        CharacterData allyData = null;
        CharacterData enemyData = null;
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character ally = CreateCharacter(allyObject, CombatTeam.Ally, new Vector3(3f, 0f, 0f));
            Character enemy = CreateCharacter(enemyObject, CombatTeam.Enemy, new Vector3(1f, 0f, 0f));
            ownerData = CreateCharacterData(CharacterGender.Male);
            allyData = CreateCharacterData(CharacterGender.Female);
            enemyData = CreateCharacterData(CharacterGender.Female);
            SetCharacterData(owner, ownerData);
            SetCharacterData(ally, allyData);
            SetCharacterData(enemy, enemyData);
            profile = SetPersonality(owner, CombatAiPersonalityKind.Lecherous);
            CombatAiPersonalityRuntime runtime = ownerObject.AddComponent<CombatAiPersonalityRuntime>();

            runtime.Refresh();

            Assert.That(runtime.Companion, Is.EqualTo(ally));
            Assert.That(owner.STRBuff, Is.EqualTo(1.2f).Within(0.001f));
            ally.transform.position = new Vector3(8f, 0f, 0f);
            runtime.Refresh();
            Assert.That(owner.STRBuff, Is.EqualTo(0.85f).Within(0.001f));
            Assert.That(runtime.TryGetSignatureTarget(out CombatMoveTarget target), Is.True);
            Assert.That(target.TargetCharacter, Is.EqualTo(ally));
        }
        finally
        {
            Destroy(profile, ownerData, allyData, enemyData, enemyObject, allyObject, ownerObject);
        }
    }

    [Test]
    public void メンヘラは庇えた盾へ一度だけ報復する()
    {
        GameObject ownerObject = new GameObject("Unstable");
        GameObject guardianObject = new GameObject("Guardian");
        GameObject attackerObject = new GameObject("Attacker");
        GameObject systemObject = new GameObject("CharacterSystem");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character guardian = CreateCharacter(guardianObject, CombatTeam.Ally, new Vector3(1f, 0f, 0f));
            Character attacker = CreateCharacter(attackerObject, CombatTeam.Enemy, new Vector3(0f, 0f, 3f));
            guardian.EquipWeapon(new Shield());
            CombatEditModeTestUtil.SetAvailableCombatSkills(guardian, new ShieldShoulderGuardSkill());
            profile = SetPersonality(owner, CombatAiPersonalityKind.Unstable);
            CombatAiPersonalityRuntime runtime = ownerObject.AddComponent<CombatAiPersonalityRuntime>();
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            system.AllyCharacters.Add(owner);
            system.AllyCharacters.Add(guardian);
            system.EnemyCharacters.Add(attacker);
            system.AssignTeamsFromLists();
            CombatEditModeTestUtil.WireVision(guardian.Vision, system);
            guardian.transform.LookAt(attacker.transform.position);
            guardian.Vision.Initialize();
            guardian.Vision.UpdateVision();
            runtime.ResetForBattle();

            owner.Health.TakeDamage(1, attacker);

            Assert.That(runtime.RevengeTarget, Is.EqualTo(guardian));
            Assert.That(runtime.TryBuildRevengePlan(out CombatAiPlan plan), Is.True);
            int hpBefore = guardian.Health.HP;
            plan.Skill.Execute(owner, plan.SkillContext);
            runtime.NotifyPlanExecuted(plan, usedSkill: true);
            Assert.That(guardian.Health.HP, Is.LessThan(hpBefore));
            Assert.That(runtime.RevengeTarget, Is.Null);
        }
        finally
        {
            Destroy(profile, systemObject, attackerObject, guardianObject, ownerObject);
        }
    }

    [Test]
    public void メンヘラは肩代わりで無傷なら盾を恨まない()
    {
        GameObject ownerObject = new GameObject("Unstable");
        GameObject guardianObject = new GameObject("Guardian");
        GameObject attackerObject = new GameObject("Attacker");
        GameObject systemObject = new GameObject("CharacterSystem");
        CombatAiPersonalityProfile profile = null;
        try
        {
            Character owner = CreateCharacter(ownerObject, CombatTeam.Ally, Vector3.zero);
            Character guardian = CreateCharacter(guardianObject, CombatTeam.Ally, new Vector3(1f, 0f, 0f));
            Character attacker = CreateCharacter(attackerObject, CombatTeam.Enemy, new Vector3(0f, 0f, 3f));
            guardian.EquipWeapon(new Shield());
            CombatEditModeTestUtil.SetAvailableCombatSkills(guardian, new ShieldShoulderGuardSkill());
            profile = SetPersonality(owner, CombatAiPersonalityKind.Unstable);
            CombatAiPersonalityRuntime runtime = ownerObject.AddComponent<CombatAiPersonalityRuntime>();
            CombatCharacterSystem system = systemObject.AddComponent<CombatCharacterSystem>();
            CombatEditModeTestUtil.WireVision(guardian.Vision, system);
            guardian.Vision.Initialize();
            guardian.Vision.UpdateVision();
            ShieldShoulderGuardEffect effect = ownerObject.AddComponent<ShieldShoulderGuardEffect>();
            effect.Initialize(guardian, owner, 0.6f, 5f);
            int ownerHpBefore = owner.Health.HP;

            owner.Health.TakeDamage(5, attacker);

            Assert.That(owner.Health.HP, Is.EqualTo(ownerHpBefore));
            Assert.That(runtime.RevengeTarget, Is.Null);
        }
        finally
        {
            Destroy(profile, systemObject, attackerObject, guardianObject, ownerObject);
        }
    }

    private static Character CreateCharacter(GameObject gameObject, CombatTeam team, Vector3 position)
    {
        Character character = gameObject.AddComponent<Character>();
        character.SetTeam(team);
        character.Health.Initialize(30);
        character.EquipWeapon(new Sword());
        gameObject.transform.position = position;
        return character;
    }

    private static CharacterData CreateCharacterData(CharacterGender gender)
    {
        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        CombatEditModeTestUtil.SetPrivateField(data, "<Gender>k__BackingField", gender);
        return data;
    }

    private static void SetLovers(CharacterData first, CharacterData second)
    {
        CombatEditModeTestUtil.SetPrivateField(first, "<Lover>k__BackingField", second);
        CombatEditModeTestUtil.SetPrivateField(second, "<Lover>k__BackingField", first);
    }

    private static void SetCharacterData(Character character, CharacterData data)
    {
        CombatEditModeTestUtil.SetPrivateField(character, "<CharacterData>k__BackingField", data);
    }

    private static CombatAiPersonalityProfile SetPersonality(Character character, CombatAiPersonalityKind kind)
    {
        CombatAiPersonalityProfile profile = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
        character.ConfigureForBattle(character.EquippedWeaponConfig, profile);
        return profile;
    }

    private static void Destroy(params Object[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        }
    }
}
