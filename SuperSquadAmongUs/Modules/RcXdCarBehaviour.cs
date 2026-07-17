using Il2CppInterop.Runtime.Attributes;
using MiraAPI.GameOptions;
using MiraAPI.Hud;
using Reactor.Utilities.Attributes;
using SuperSquadAmongUs.Options.Roles.Impostor;
using UnityEngine;

namespace SuperSquadAmongUs.Modules;

/// <summary>
/// Behaviour component for the RC-XD car. Supports two modes: driver (physics-simulated on the
/// deployer's client, sends throttled position updates) and remote (interpolates toward the latest
/// received position on observer clients). Both modes use joystick input (driver) or network updates
/// (observers) and flip the sprite based on movement direction.
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

    /// <summary>
    /// The impostor who deployed and is driving the car.
    /// </summary>
    public PlayerControl? Owner => _owner;

    /// <summary>
    /// Initializes the car with a deployer. On the deployer's client, adds physics components and
    /// sets up collision with the ship. On observer clients, sets up interpolation-only mode.
    /// </summary>
    /// <param name="owner">The impostor driving the car.</param>
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

    /// <summary>
    /// Updates the car's target position (used by remote clients to interpolate toward the latest
    /// network position).
    /// </summary>
    /// <param name="pos">The new target position.</param>
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
