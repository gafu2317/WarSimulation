using NUnit.Framework;
using UnityEngine;

public sealed class CombatSkillEvaluatorTests
{
    [Test]
    public void Evaluate_AllowsSingleTargetSkillWhenHorizontalRangeIsInsideEvenIfVerticalOffsetIsLarge()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withEnemy: true, registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            fixture.EnemyGo.transform.position = new Vector3(1.5f, 10f, 0f);
            var skill = new EvaluatorTestSkill(SkillTargetKind.Enemy, maxRange: 2f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, fixture.Enemy));

            Assert.That(result.CanUse, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RejectsSingleTargetSkillWhenHorizontalRangeIsOutside()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withEnemy: true, registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            fixture.EnemyGo.transform.position = new Vector3(2.1f, 0f, 0f);
            var skill = new EvaluatorTestSkill(SkillTargetKind.Enemy, maxRange: 2f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, fixture.Enemy));

            Assert.That(result.CanUse, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("target out of range"));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_AllowsPointSkillWithoutResolvedTargets()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create();
        try
        {
            var skill = new EvaluatorTestSkill(SkillTargetKind.Point, maxRange: 5f, areaRadius: 3f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForPoint(fixture.Owner, new Vector3(3f, 0f, 0f)));

            Assert.That(result.CanUse, Is.True);
            Assert.That(result.HasAreaPreview, Is.True);
            Assert.That(result.AreaRadius, Is.EqualTo(3f));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RejectsAreaSkillWithoutResolvedTargets()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create();
        try
        {
            var skill = new EvaluatorTestSkill(SkillTargetKind.Area, maxRange: 5f, areaRadius: 2f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForPoint(fixture.Owner, new Vector3(2f, 0f, 0f)));

            Assert.That(result.CanUse, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("no targets in area"));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RecognizedEnemiesUsesResolvedTargetsAndPreviewData()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withEnemy: true, withSecondEnemy: true, registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            var skill = new EvaluatorTestSkill(SkillTargetKind.RecognizedEnemies, maxRange: 0f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, null));

            Assert.That(result.CanUse, Is.True);
            Assert.That(result.ResolvedTargets.Count, Is.EqualTo(2));
            Assert.That(result.HasRangePreview, Is.False);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RejectsRecognizedEnemiesWhenNoneExist()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            var skill = new EvaluatorTestSkill(SkillTargetKind.RecognizedEnemies, maxRange: 0f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, null));

            Assert.That(result.CanUse, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("no enemies"));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RosarySacrificeThunder_UsesRecognizedEnemies()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withEnemy: true, withSecondEnemy: true, registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            fixture.Owner.Vision.UpdateVision();
            fixture.SecondEnemy.StatusEffects.ApplyStealth(5f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                new RosarySacrificeThunderSkill(),
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, null));

            Assert.That(result.CanUse, Is.True);
            Assert.That(result.ResolvedTargets.Count, Is.EqualTo(2));
            Assert.That(result.ResolvedTargets, Has.Member(fixture.Enemy));
            Assert.That(result.ResolvedTargets, Has.Member(fixture.SecondEnemy));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_RejectsEnemyTargetWhenNotRecognized()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withEnemy: true, registerSystem: true);
        try
        {
            CombatEditModeTestUtil.WireVision(fixture.Owner.Vision, fixture.CharacterSystem);
            fixture.Owner.Vision.Initialize();
            fixture.Enemy.StatusEffects.ApplyStealth(5f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                new EvaluatorTestSkill(SkillTargetKind.Enemy, maxRange: 5f),
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, fixture.Enemy));

            Assert.That(result.CanUse, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo("enemy not recognized"));
        }
        finally
        {
            fixture.Destroy();
        }
    }

    [Test]
    public void Evaluate_AllowsBoundAllyAsSupportTarget()
    {
        SkillEvaluatorFixture fixture = SkillEvaluatorFixture.Create(withAlly: true);
        try
        {
            fixture.Ally.StatusEffects.ApplyBind(5f);
            var skill = new EvaluatorTestSkill(SkillTargetKind.AllyOrSelf, maxRange: 4f);

            CombatSkillEvaluationResult result = CombatSkillEvaluator.Evaluate(
                skill,
                CombatSkillEvaluationRequest.ForTarget(fixture.Owner, fixture.Ally));

            Assert.That(result.CanUse, Is.True);
        }
        finally
        {
            fixture.Destroy();
        }
    }

    private sealed class EvaluatorTestSkill : SkillBase
    {
        private readonly SkillTargetKind _targetKind;
        private readonly float _maxRange;
        private readonly float _areaRadius;

        public EvaluatorTestSkill(SkillTargetKind targetKind, float maxRange, float areaRadius = 0f)
        {
            _targetKind = targetKind;
            _maxRange = maxRange;
            _areaRadius = areaRadius;
        }

        public override string Name => "EvaluatorTest";
        public override SkillTargetKind TargetKind => _targetKind;
        public override float MaxRange => _maxRange;
        public override float AreaRadius => _areaRadius;

        public override void Execute(Character self, SkillExecutionContext context)
        {
        }
    }

    private sealed class SkillEvaluatorFixture
    {
        public GameObject OwnerGo;
        public GameObject EnemyGo;
        public GameObject SecondEnemyGo;
        public GameObject AllyGo;
        public GameObject CharacterSystemGo;
        public Character Owner;
        public Character Enemy;
        public Character SecondEnemy;
        public Character Ally;
        public CombatCharacterSystem CharacterSystem;

        public static SkillEvaluatorFixture Create(
            bool withEnemy = false,
            bool withSecondEnemy = false,
            bool withAlly = false,
            bool registerSystem = false)
        {
            var fixture = new SkillEvaluatorFixture
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

            if (registerSystem)
            {
                fixture.CharacterSystemGo = new GameObject("CharacterSystem");
                fixture.CharacterSystem = fixture.CharacterSystemGo.AddComponent<CombatCharacterSystem>();
                fixture.CharacterSystem.AllyCharacters.Add(fixture.Owner);
                if (fixture.Ally != null) fixture.CharacterSystem.AllyCharacters.Add(fixture.Ally);
                if (fixture.Enemy != null) fixture.CharacterSystem.EnemyCharacters.Add(fixture.Enemy);
                if (fixture.SecondEnemy != null) fixture.CharacterSystem.EnemyCharacters.Add(fixture.SecondEnemy);
                fixture.CharacterSystem.AssignTeamsFromLists();
            }

            return fixture;
        }

        public void Destroy()
        {
            if (CharacterSystemGo != null) Object.DestroyImmediate(CharacterSystemGo);
            if (AllyGo != null) Object.DestroyImmediate(AllyGo);
            if (SecondEnemyGo != null) Object.DestroyImmediate(SecondEnemyGo);
            if (EnemyGo != null) Object.DestroyImmediate(EnemyGo);
            if (OwnerGo != null) Object.DestroyImmediate(OwnerGo);
        }
    }
}
