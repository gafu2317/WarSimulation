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
            var skill = new TargetValidationSkill(SkillTargetKind.Self, maxRange: 0f);

            bool canExecute = CombatSkillEvaluator.Evaluate(
                fixture.Owner,
                skill,
                SkillExecutionContext.ForSelf(fixture.Owner)).CanUse;

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
            var skill = new TargetValidationSkill(SkillTargetKind.Point, maxRange: 5f);

            bool canExecuteWithoutPoint = CombatSkillEvaluator.Evaluate(
                fixture.Owner,
                skill,
                SkillExecutionContext.None).CanUse;
            bool canExecuteWithPoint = CombatSkillEvaluator.Evaluate(
                fixture.Owner,
                skill,
                SkillExecutionContext.ForPoint(fixture.OwnerGo.transform.position + Vector3.forward * 3f)).CanUse;

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
            var skill = new TargetValidationSkill(SkillTargetKind.Point, maxRange: 5f);

            bool canExecute = CombatSkillEvaluator.Evaluate(
                fixture.Owner,
                skill,
                SkillExecutionContext.ForPoint(fixture.OwnerGo.transform.position + Vector3.forward * 7f)).CanUse;

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
            var skill = new TargetValidationSkill(SkillTargetKind.AllEnemies, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForTargets(
                new[] { fixture.Enemy, fixture.SecondEnemy });

            bool canExecute = CombatSkillEvaluator.Evaluate(fixture.Owner, skill, context).CanUse;

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
            var skill = new TargetValidationSkill(SkillTargetKind.AllEnemies, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForTargets(
                new[] { fixture.Enemy, fixture.Ally });

            bool canExecute = CombatSkillEvaluator.Evaluate(fixture.Owner, skill, context).CanUse;

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
            var skill = new TargetValidationSkill(SkillTargetKind.Area, maxRange: 5f);
            SkillExecutionContext context = SkillExecutionContext.ForPoint(
                fixture.OwnerGo.transform.position + Vector3.forward * 2f);

            bool canExecute = CombatSkillEvaluator.Evaluate(fixture.Owner, skill, context).CanUse;

            Assert.That(canExecute, Is.False);
        }
        finally
        {
            fixture.Destroy();
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
