using UnityEngine;

public class CharacterAnimationTester : MonoBehaviour
{
    [SerializeField] private CharacterAnimationController _controller;
    [SerializeField, Range(0f, 2f)] private float _speed;

    private void Update()
    {
        _controller.UpdateWalkAnimationSpeed(_speed);
    }
}
