using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using Reactor.Utilities.Attributes;
using SuperSquadAmongUs.Options.Roles.Impostor;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// The RC-XD car's behaviour. Driver mode (deployer's client) simulates physics from joystick input
/// and sends throttled position updates; remote clients run in interpolation-only mode instead,
/// chasing the latest received position. Both modes flip the sprite based on movement direction.
/// </summary>
[RegisterInIl2Cpp]
public sealed class RcXdCarBehaviour(IntPtr cppPtr) : MonoBehaviour(cppPtr)
{
    private PlayerControl? _owner;
    private Rigidbody2D? _rigidbody;
    private SpriteRenderer? _renderer;
    private Vector2 _targetPosition;
    private float _sendAccumulator;
    private Vector2 _lastSentPosition;
    private Vector2 _lastFlipDirection = Vector2.right;

    public PlayerControl? Owner => _owner;

    public void Initialize(PlayerControl owner)
    {
        _owner = owner;
        _renderer = GetComponent<SpriteRenderer>();
        _targetPosition = transform.position;
        _lastSentPosition = transform.position;

        if (!owner.AmOwner)
        {
            return;
        }

        _rigidbody = gameObject.AddComponent<Rigidbody2D>();
        _rigidbody.gravityScale = 0f;
        _rigidbody.freezeRotation = true;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.3f;

        gameObject.layer = owner.gameObject.layer;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player == null)
            {
                continue;
            }

            var playerCollider = player.Collider;
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(collider, playerCollider);
            }
        }
    }

    public void SetTargetPosition(Vector2 pos)
    {
        _targetPosition = pos;
    }

    private void FixedUpdate()
    {
        if (_rigidbody == null || _owner == null)
        {
            return;
        }

        if (!HudManager.InstanceExists || HudManager.Instance.joystick == null)
        {
            _rigidbody.velocity = Vector2.zero;
            return;
        }

        var direction = HudManager.Instance.joystick.DeltaL;
        if (direction.magnitude > 0)
        {
            var speed = _owner.MyPhysics.Speed * OptionGroupSingleton<RcXdOptions>.Instance.CarSpeedMultiplier;
            _rigidbody.velocity = direction.normalized * speed;
        }
        else
        {
            _rigidbody.velocity = Vector2.zero;
        }

        _sendAccumulator += Time.fixedDeltaTime;
        var currentPos = (Vector2)transform.position;
        var distance = Vector2.Distance(currentPos, _lastSentPosition);

        if (_sendAccumulator >= 0.1f && distance > 0.01f)
        {
            RcXdCar.RpcMoveCar(_owner, currentPos.x, currentPos.y);
            _sendAccumulator = 0f;
            _lastSentPosition = currentPos;
        }
    }

    private void Update()
    {
        if (_owner == null)
        {
            return;
        }

        if (_rigidbody == null)
        {
            var pos = (Vector2)transform.position;
            var moveStep = _owner.MyPhysics.Speed * OptionGroupSingleton<RcXdOptions>.Instance.CarSpeedMultiplier * Time.deltaTime * 1.5f;
            transform.position = Vector2.MoveTowards(pos, _targetPosition, moveStep);
        }

        // Safety net for remote clients in case the deployer's fizzle/detonate RPC races the meeting.
        if (MeetingHud.Instance != null)
        {
            RcXdCar.EnsureDestroyedLocally();
        }
    }

    private void LateUpdate()
    {
        if (_renderer == null)
        {
            return;
        }

        var pos = transform.position;
        pos.z = pos.y / 1000f;
        transform.position = pos;

        if (_rigidbody != null)
        {
            var velocity = _rigidbody.velocity;
            _lastFlipDirection = velocity.x switch
            {
                < -0.01f => Vector2.left,
                > 0.01f => Vector2.right,
                _ => _lastFlipDirection,
            };
            _renderer.flipX = _lastFlipDirection.x < 0;
        }
        else
        {
            var direction = _targetPosition - (Vector2)transform.position;
            _lastFlipDirection = direction.x switch
            {
                < -0.01f => Vector2.left,
                > 0.01f => Vector2.right,
                _ => _lastFlipDirection,
            };
            _renderer.flipX = _lastFlipDirection.x < 0;
        }
    }
}
