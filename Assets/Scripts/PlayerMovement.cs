﻿using UnityEngine;


public class PlayerMovement : MonoBehaviour
{
    [SerializeField] float speed;
    [SerializeField] private SpriteRenderer sprite;
    private Vector2 _direction;
    private Vector2 _targetPos;
    private Animator _animator;

    public enum State
    {
        UP,
        DOWN,
        LEFT,
        RIGHT
    };

    private State _stateDir;

    void Start()
    {
        _animator = GetComponent<Animator>();
    }
    void Update()
    {

        TakeInput();
        Move();
    }

    private void Move() //Moves the player
    {
        transform.Translate(_direction * (speed * Time.deltaTime));
        if (_direction.x != 0 || _direction.y != 0)                          // PUT DEADZONE HERE
        {
            _animator.SetTrigger("Walk");
            _animator.ResetTrigger("Idle");
        }
        else
        {
            _animator.SetTrigger("Idle");
            _animator.ResetTrigger("Walk");
        }
    }

    private void TakeInput() // Takes input to move the player
    {
        _direction = Vector2.zero;
        if (Input.GetKey(KeyCode.W))
        {
            _direction += Vector2.up;
          _stateDir = State.UP;
        }
        if (Input.GetKey(KeyCode.S))
        {
            _direction += Vector2.down; 
            _stateDir = State.DOWN;
        }
        if (Input.GetKey(KeyCode.A))
        {
            _direction += Vector2.left; 
            _stateDir = State.LEFT;
        }
        if (Input.GetKey(KeyCode.D))
        {
            _direction += Vector2.right; 
            _stateDir = State.RIGHT;
        }
    }

}
