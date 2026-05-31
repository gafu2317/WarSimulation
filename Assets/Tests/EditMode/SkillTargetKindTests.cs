using NUnit.Framework;
using UnityEngine;

public sealed class SkillTargetKindTests
{
    [Test]
    public void CanExecuteSkill_AllowsSelfSkillWithoutExplicitTarget()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create();
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.Self, maxRange: 0f);

            bool canExecute = personality.CanExecute(skill, SkillExecutionContext.ForSelf(fixture.Owner));

            Assert.That(canExecute, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CanExecuteSkill_RequiresPointForPointSkill()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create();
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.Point, maxRange: 5f);

            bool canExecuteWithoutPoint = personality.CanExecute(skill, SkillExecutionContext.None);
            bool canExecuteWithPoint = personality.CanExecute(
                skill,
                SkillExecutionContext.ForPoint(fixture.OwnerGo.transform.position + Vector3.forward * 3f));

            Assert.That(canExecuteWithoutPoint, Is.False);
            Assert.That(canExecuteWithPoint, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CanExecuteSkill_RejectsOutOfRangePointSkill()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create();
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.Point, maxRange: 5f);

            bool canExecute = personality.CanExecute(
                skill,
                SkillExecutionContext.ForPoint(fixture.OwnerGo.transform.position + Vector3.forward * 7f));

            Assert.That(canExecute, Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CanExecuteSkill_AllowsAllEnemiesWhenAllTargetsAreValidEnemies()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create(withEnemy: true, withSecondEnemy: true);
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.AllEnemies, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForTargets(
                new[] { fixture.Enemy, fixture.SecondEnemy });

            bool canExecute = personality.CanExecute(skill, context);

            Assert.That(canExecute, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CanExecuteSkill_RejectsAllEnemiesWhenAnyTargetIsAnAlly()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create(withEnemy: true, withAlly: true);
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.AllEnemies, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForTargets(
                new[] { fixture.Enemy, fixture.Ally });

            bool canExecute = personality.CanExecute(skill, context);

            Assert.That(canExecute, Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void CanExecuteSkill_RejectsAreaSkillWithoutResolvedTargets()
    {
        SkillTargetFixture fixture = SkillTargetFixture.Create();
        try
        {
            var personality = fixture.OwnerGo.AddComponent<TargetValidationPersonality>();
            var skill = new TargetValidationSkill(SkillTargetKind.Area, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForPoint(
                fixture.OwnerGo.transform.position + Vector3.forward * 2f);

            bool canExecute = personality.CanExecute(skill, context);

            Assert.That(canExecute, Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private sealed class TargetValidationPersonality : PersonalityBase
    {
        public override CombatAiPlan DecidePlan() => CombatAiPlan.None;

        public bool CanExecute(SkillBase skill, SkillExecutionContext context)
        {
            return CanExecuteSkill(skill, context);
        }
    }

    private sealed class TargetValidationSkill : SkillBase
    {
        private readonly SkillTargetKind _targetKind;
        private readonly float _maxRange;

        public TargetValidationSkill(SkillTargetKind targetKind, float maxRange)
        {
            _targetKind = targetKind;
            _maxRange = maxRange;
        }

        public override string Name => "TargetValidation";
        public override SkillTargetKind TargetKind => _targetKind;
        public override float MaxRange => _maxRange;

        public override void Execute(Character self, SkillExecutionContext context)
        {
        }
    }

    private sealed class SkillTargetFixture
    {
        public GameObject OwnerGo;
        public GameObject EnemyGo;
        public GameObject SecondEnemyGo;
        public GameObject AllyGo;
        public Character Owner;
        public Character Enemy;
        public Character SecondEnemy;
        public Character Ally;

        public static SkillTargetFixture Create(
            bool withEnemy = false,
            bool withSecondEnemy = false,
            bool withAlly = false)
        {
            var fixture = new SkillTargetFixture
            {
                OwnerGo = new GameObject("Owner"),
            };

            fixture.Owner = fixture.OwnerGo.AddComponent<Character>();
            fixture.Owner.SetTeam(CombatTeam.Ally);
            fixture.Owner.Health.Initialize(30);

            if (withEnemy)
            {
                fixture.EnemyGo = new GameObject("Enemy");
                fixture.Enemy = fixture.EnemyGo.AddComponent<Character>();
                fixture.Enemy.SetTeam(CombatTeam.Enemy);
                fixture.Enemy.Health.Initialize(30);
                fixture.EnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.forward * 2f;
            }

            if (withSecondEnemy)
            {
                fixture.SecondEnemyGo = new GameObject("Enemy2");
                fixture.SecondEnemy = fixture.SecondEnemyGo.AddComponent<Character>();
                fixture.SecondEnemy.SetTeam(CombatTeam.Enemy);
                fixture.SecondEnemy.Health.Initialize(30);
                fixture.SecondEnemyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.right * 2f;
            }

            if (withAlly)
            {
                fixture.AllyGo = new GameObject("Ally");
                fixture.Ally = fixture.AllyGo.AddComponent<Character>();
                fixture.Ally.SetTeam(CombatTeam.Ally);
                fixture.Ally.Health.Initialize(30);
                fixture.AllyGo.transform.position = fixture.OwnerGo.transform.position + Vector3.left * 2f;
            }

            return fixture;
        }

        public void Destroy()
        {
            if (AllyGo != null) Object.DestroyImmediate(AllyGo);
            if (SecondEnemyGo != null) Object.DestroyImmediate(SecondEnemyGo);
            if (EnemyGo != null) Object.DestroyImmediate(EnemyGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
        }
    }
}
