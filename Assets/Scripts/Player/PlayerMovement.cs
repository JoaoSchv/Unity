using System.Collections;
using UnityEngine;
using System.Reflection;
using DG.Tweening;

public class PlayerMovement : MonoBehaviour
{
	public PlayerData Data;

	#region COMPONENTS
	public Rigidbody2D RB { get; private set; }
	public PlayerAnimator AnimHandler { get; private set; }
	#endregion

	#region STATE PARAMETERS
	public bool IsFacingRight { get; private set; }
	public bool IsJumping { get; private set; }
	public bool IsWallJumping { get; private set; }
	public bool IsDashing { get; private set; }
	public bool IsSliding { get; private set; }
	public float LastOnGroundTime { get; private set; }
	public float LastOnWallTime { get; private set; }
	public float LastOnWallRightTime { get; private set; }
	public float LastOnWallLeftTime { get; private set; }

	//Jump
	private bool _isJumpCut;
	private bool _isJumpFalling;

	//Wall Jump
	private float _wallJumpStartTime;
	private int _lastWallJumpDir;

	//Dash
	private int _dashesLeft;
	private bool _dashRefilling;
	private Vector2 _lastDashDir;
	private bool _isDashAttacking;

	#endregion

	#region INPUT PARAMETERS
	private Vector2 _moveInput;

	public float LastPressedJumpTime { get; private set; }
	public float LastPressedDashTime { get; private set; }
	#endregion

	#region CHECK PARAMETERS
	[Header("Checks")]
	[SerializeField] private Transform _groundCheckPoint;
	[SerializeField] private Vector2 _groundCheckSize = new Vector2(0.49f, 0.03f);
	[Space(5)]
	[SerializeField] private Transform _frontWallCheckPoint;
	[SerializeField] private Transform _backWallCheckPoint;
	[SerializeField] private Vector2 _wallCheckSize = new Vector2(0.5f, 1f);
	#endregion

	[Header("Ripple / Camera (optional)")]
		[SerializeField] private Camera rippleCamera; // arraste a Main Camera aqui no Inspector
		[SerializeField] private float rippleStrength = 1f; // parâmetro genérico para passar ao efeito

	#region LAYERS & TAGS
	[Header("Layers & Tags")]
	[SerializeField] private LayerMask _groundLayer;
	#endregion

	private void Awake()
	{
		RB = GetComponent<Rigidbody2D>();
		AnimHandler = GetComponent<PlayerAnimator>();
	}

	/// <summary>
/// Tenta acionar o RippleEffect da câmera de várias formas:
/// - SendMessage (AddRipple / EmitRipple / AddSplash)
/// - Reflexão para métodos comuns (AddRipple, Emit, Splash)
/// - Caso não haja nada, apenas retorna sem erro.
/// Ajuste a conversão de posição se o seu RippleEffect espera coordenadas de tela / viewport.
/// </summary>
private void TriggerRipple(float optionalStrength = -1f)
{
    if (rippleCamera == null)
    {
        rippleCamera = Camera.main;
        if (rippleCamera == null) return;
    }

    float strengthToUse = optionalStrength > 0 ? optionalStrength : rippleStrength;

    // Posição do jogador em viewport (x,y) — muitos efeitos esperam coords normalizadas
    Vector3 vp = rippleCamera.WorldToViewportPoint(transform.position);
    Vector2 vp2 = new Vector2(vp.x, vp.y);
    Vector3 screenPos = rippleCamera.WorldToScreenPoint(transform.position);

    // 1) tentativa rápida usando SendMessage (não exige conhecer a assinatura)
    // passamos diferentes formatos — o efeito aceitará o que souber.
    rippleCamera.gameObject.SendMessage("AddRipple", vp2, SendMessageOptions.DontRequireReceiver);
    rippleCamera.gameObject.SendMessage("AddRipple", vp, SendMessageOptions.DontRequireReceiver);
    rippleCamera.gameObject.SendMessage("AddRipple", screenPos, SendMessageOptions.DontRequireReceiver);

    rippleCamera.gameObject.SendMessage("EmitRipple", vp2, SendMessageOptions.DontRequireReceiver);
    rippleCamera.gameObject.SendMessage("Emit", vp2, SendMessageOptions.DontRequireReceiver);
    rippleCamera.gameObject.SendMessage("Splash", vp2, SendMessageOptions.DontRequireReceiver);

    // 2) fallback por reflexão: procura métodos comuns no componente RippleEffect
    Component rippleComp = rippleCamera.GetComponent("RippleEffect");
    if (rippleComp == null)
    {
        // às vezes o script tem outro nome; tentar encontrar por heurística:
        foreach (var comp in rippleCamera.GetComponents<Component>())
        {
            var t = comp.GetType();
            if (t.Name.ToLower().Contains("ripple") || t.Name.ToLower().Contains("water"))
            {
                rippleComp = comp;
                break;
            }
        }
    }

    if (rippleComp != null)
    {
        var t = rippleComp.GetType();

        // tenta AddRipple(Vector2/Vector3) ou Emit(Vector2/float)
        MethodInfo m;

        m = t.GetMethod("AddRipple", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            // tenta várias assinaturas possíveis
            try { m.Invoke(rippleComp, new object[] { vp2 }); } catch { }
            try { m.Invoke(rippleComp, new object[] { vp }); } catch { }
            try { m.Invoke(rippleComp, new object[] { screenPos }); } catch { }
            try { m.Invoke(rippleComp, new object[] { vp2, strengthToUse }); } catch { }
        }

        m = t.GetMethod("Emit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            try { m.Invoke(rippleComp, new object[] { vp2 }); } catch { }
            try { m.Invoke(rippleComp, new object[] { strengthToUse }); } catch { }
            try { m.Invoke(rippleComp, new object[] { vp2, strengthToUse }); } catch { }
        }

        // método genérico "Splash"
        m = t.GetMethod("Splash", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            try { m.Invoke(rippleComp, new object[] { vp2, strengthToUse }); } catch { }
        }
    }
}


	private void Start()
	{
		SetGravityScale(Data.gravityScale);
		IsFacingRight = true;

		string[] names = Input.GetJoystickNames();
		for (int i = 0; i < names.Length; i++)
		{
			Debug.Log($"Joystick {i + 1}: �{names[i]}�");
		}

		if (names.Length == 0)
			Debug.Log("Nenhum joystick detectado.");
	}

	private void Update()
	{
		#region TIMERS
		LastOnGroundTime -= Time.deltaTime;
		LastOnWallTime -= Time.deltaTime;
		LastOnWallRightTime -= Time.deltaTime;
		LastOnWallLeftTime -= Time.deltaTime;

		LastPressedJumpTime -= Time.deltaTime;
		LastPressedDashTime -= Time.deltaTime;
		#endregion

		#region INPUT HANDLER
		_moveInput.x = Input.GetAxisRaw("Horizontal");
		_moveInput.y = Input.GetAxisRaw("Vertical");

		if (_moveInput.x != 0)
			CheckDirectionToFace(_moveInput.x > 0);

		if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) ||
		   Input.GetKeyDown(KeyCode.UpArrow) || AnyJoystickButtonDown(0))
		{
			OnJumpInput();
		}

		if (Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.Space) ||
		    Input.GetKeyUp(KeyCode.UpArrow) || AnyJoystickButtonUp(0))
		{
			OnJumpUpInput();
		}

		if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftShift) ||
		    Input.GetKeyDown(KeyCode.K) || AnyJoystickButtonDown(1) || AnyJoystickButtonDown(5))
		{
			OnDashInput();
		}
		#endregion

		#region COLLISION CHECKS
		if (!IsDashing && !IsJumping)
		{
			//Ground Check
			if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer))
			{
				if (LastOnGroundTime < -0.1f)
				{
					AnimHandler.justLanded = true;
				}

				LastOnGroundTime = Data.coyoteTime;
			}

			//Right Wall Check
			if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)
					|| (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)) && !IsWallJumping)
				LastOnWallRightTime = Data.coyoteTime;

			//Right Wall Check
			if (((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)
				|| (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)) && !IsWallJumping)
				LastOnWallLeftTime = Data.coyoteTime;

			LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
		}
		#endregion

		#region JUMP CHECKS
		if (IsJumping && RB.velocity.y < 0)
		{
			IsJumping = false;

			_isJumpFalling = true;
		}

		if (IsWallJumping && Time.time - _wallJumpStartTime > Data.wallJumpTime)
		{
			IsWallJumping = false;
		}

		if (LastOnGroundTime > 0 && !IsJumping && !IsWallJumping)
		{
			_isJumpCut = false;

			_isJumpFalling = false;
		}

		if (!IsDashing)
		{
			//Jump
			if (CanJump() && LastPressedJumpTime > 0)
			{
				IsJumping = true;
				IsWallJumping = false;
				_isJumpCut = false;
				_isJumpFalling = false;
				Jump();

				AnimHandler.startedJumping = true;
			}
			//WALL JUMP
			else if (CanWallJump() && LastPressedJumpTime > 0)
			{
				IsWallJumping = true;
				IsJumping = false;
				_isJumpCut = false;
				_isJumpFalling = false;

				_wallJumpStartTime = Time.time;
				_lastWallJumpDir = (LastOnWallRightTime > 0) ? -1 : 1;

				WallJump(_lastWallJumpDir);
			}
		}
		#endregion

		#region DASH CHECKS
		if (CanDash() && LastPressedDashTime > 0)
		{
			Sleep(Data.dashSleepTime);

			if (_moveInput != Vector2.zero)
				_lastDashDir = _moveInput;
			else
				_lastDashDir = IsFacingRight ? Vector2.right : Vector2.left;



			IsDashing = true;
			IsJumping = false;
			IsWallJumping = false;
			_isJumpCut = false;

			StartCoroutine(nameof(StartDash), _lastDashDir);
		}
		#endregion

		#region SLIDE CHECKS
		if (CanSlide() && ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0)))
			IsSliding = true;
		else
			IsSliding = false;
		#endregion

		#region GRAVITY
		if (!_isDashAttacking)
		{
			if (IsSliding)
			{
				SetGravityScale(0);
			}
			else if (RB.velocity.y < 0 && _moveInput.y < 0)
			{
				SetGravityScale(Data.gravityScale * Data.fastFallGravityMult);
				RB.velocity = new Vector2(RB.velocity.x, Mathf.Max(RB.velocity.y, -Data.maxFastFallSpeed));
			}
			else if (_isJumpCut)
			{
				SetGravityScale(Data.gravityScale * Data.jumpCutGravityMult);
				RB.velocity = new Vector2(RB.velocity.x, Mathf.Max(RB.velocity.y, -Data.maxFallSpeed));
			}
			else if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(RB.velocity.y) < Data.jumpHangTimeThreshold)
			{
				SetGravityScale(Data.gravityScale * Data.jumpHangGravityMult);
			}
			else if (RB.velocity.y < 0)
			{
				SetGravityScale(Data.gravityScale * Data.fallGravityMult);
				RB.velocity = new Vector2(RB.velocity.x, Mathf.Max(RB.velocity.y, -Data.maxFallSpeed));
			}
			else
			{
				SetGravityScale(Data.gravityScale);
			}
		}
		else
		{
			SetGravityScale(0);
		}
		#endregion
	}

	private void FixedUpdate()
	{
		//Handle Run
		if (!IsDashing)
		{
			if (IsWallJumping)
				Run(Data.wallJumpRunLerp);
			else
				Run(1);
		}
		else if (_isDashAttacking)
		{
			Run(Data.dashEndRunLerp);
		}

		//Handle Slide
		if (IsSliding)
			Slide();
	}

	#region INPUT CALLBACKS
	public void OnJumpInput()
	{
		LastPressedJumpTime = Data.jumpInputBufferTime;
	}

	public void OnJumpUpInput()
	{
		if (CanJumpCut() || CanWallJumpCut())
			_isJumpCut = true;
	}

	public void OnDashInput()
	{
		LastPressedDashTime = Data.dashInputBufferTime;
	}
	#endregion

	#region GENERAL METHODS
	public void SetGravityScale(float scale)
	{
		RB.gravityScale = scale;
	}

	private void Sleep(float duration)
	{
		StartCoroutine(nameof(PerformSleep), duration);
	}

	private IEnumerator PerformSleep(float duration)
	{
		Time.timeScale = 0;
		yield return new WaitForSecondsRealtime(duration); 
		Time.timeScale = 1;
	}
	#endregion

	//MOVEMENT METHODS
	#region RUN METHODS
	private void Run(float lerpAmount)
	{
		float targetSpeed = _moveInput.x * Data.runMaxSpeed;
		targetSpeed = Mathf.Lerp(RB.velocity.x, targetSpeed, lerpAmount);

		#region Calculate AccelRate
		float accelRate;

		if (LastOnGroundTime > 0)
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount : Data.runDeccelAmount;
		else
			accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? Data.runAccelAmount * Data.accelInAir : Data.runDeccelAmount * Data.deccelInAir;
		#endregion

		#region Add Bonus Jump Apex Acceleration
		if ((IsJumping || IsWallJumping || _isJumpFalling) && Mathf.Abs(RB.velocity.y) < Data.jumpHangTimeThreshold)
		{
			accelRate *= Data.jumpHangAccelerationMult;
			targetSpeed *= Data.jumpHangMaxSpeedMult;
		}
		#endregion

		#region Conserve Momentum
		if (Data.doConserveMomentum && Mathf.Abs(RB.velocity.x) > Mathf.Abs(targetSpeed) && Mathf.Sign(RB.velocity.x) == Mathf.Sign(targetSpeed) && Mathf.Abs(targetSpeed) > 0.01f && LastOnGroundTime < 0)
		{
			accelRate = 0;
		}
		#endregion

		float speedDif = targetSpeed - RB.velocity.x;

		float movement = speedDif * accelRate;

		RB.AddForce(movement * Vector2.right, ForceMode2D.Force);
	}

	private void Turn()
	{
		Vector3 scale = transform.localScale;
		scale.x *= -1;
		transform.localScale = scale;

		IsFacingRight = !IsFacingRight;
	}
	#endregion

	#region JUMP METHODS
	private void Jump()
	{
		LastPressedJumpTime = 0;
		LastOnGroundTime = 0;

		#region Perform Jump
		float force = Data.jumpForce;
		if (RB.velocity.y < 0)
			force -= RB.velocity.y;

		RB.AddForce(Vector2.up * force, ForceMode2D.Impulse);
		#endregion
	}

	private void WallJump(int dir)
	{
		LastPressedJumpTime = 0;
		LastOnGroundTime = 0;
		LastOnWallRightTime = 0;
		LastOnWallLeftTime = 0;

		#region Perform Wall Jump
		Vector2 force = new Vector2(Data.wallJumpForce.x, Data.wallJumpForce.y);
		force.x *= dir; 

		if (Mathf.Sign(RB.velocity.x) != Mathf.Sign(force.x))
			force.x -= RB.velocity.x;

		if (RB.velocity.y < 0) 
			force.y -= RB.velocity.y;

		RB.AddForce(force, ForceMode2D.Impulse);
		#endregion
	}
	#endregion

	#region DASH METHODS
	//Dash Coroutine
	private IEnumerator StartDash(Vector2 dir)
	{

		LastOnGroundTime = 0;
		LastPressedDashTime = 0;

		float startTime = Time.time;

		_dashesLeft--;
		_isDashAttacking = true;

		SetGravityScale(0);
		TriggerRipple();

		while (Time.time - startTime <= Data.dashAttackTime)
		{
			RB.velocity = dir.normalized * Data.dashSpeed;
			yield return null;
		}

		startTime = Time.time;

		_isDashAttacking = false;

		SetGravityScale(Data.gravityScale);
		RB.velocity = Data.dashEndSpeed * dir.normalized;

		while (Time.time - startTime <= Data.dashEndTime)
		{
			yield return null;
		}

		 if (Camera.main != null)
    {
        // finalize tweens on camera and shake a bit (se DOTween estiver no projeto)
        Camera.main.transform.DOComplete();
        Camera.main.transform.DOShakePosition(.2f, .5f, 14, 90, false, true);
    }

    // opcional: pequeno ripple ao terminar o dash também
    TriggerRipple(0.6f);

		//Dash over
		IsDashing = false;
	}

	private IEnumerator RefillDash(int amount)
	{
		_dashRefilling = true;
		yield return new WaitForSeconds(Data.dashRefillTime);
		_dashRefilling = false;
		_dashesLeft = Mathf.Min(Data.dashAmount, _dashesLeft + 1);
	}
	#endregion

	#region OTHER MOVEMENT METHODS
	private void Slide()
	{
		if (RB.velocity.y > 0)
		{
			RB.AddForce(-RB.velocity.y * Vector2.up, ForceMode2D.Impulse);
		}

		float speedDif = Data.slideSpeed - RB.velocity.y;
		float movement = speedDif * Data.slideAccel;
		movement = Mathf.Clamp(movement, -Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime), Mathf.Abs(speedDif) * (1 / Time.fixedDeltaTime));

		RB.AddForce(movement * Vector2.up);
	}
	#endregion


	#region CHECK METHODS
	public void CheckDirectionToFace(bool isMovingRight)
	{
		if (isMovingRight != IsFacingRight)
			Turn();
	}

	private bool CanJump()
	{
		return LastOnGroundTime > 0 && !IsJumping;
	}

	private bool CanWallJump()
	{
		return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (!IsWallJumping ||
			 (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
	}

	private bool CanJumpCut()
	{
		return IsJumping && RB.velocity.y > 0;
	}

	private bool CanWallJumpCut()
	{
		return IsWallJumping && RB.velocity.y > 0;
	}

	private bool CanDash()
	{
		if (!IsDashing && _dashesLeft < Data.dashAmount && LastOnGroundTime > 0 && !_dashRefilling )
		{
			StartCoroutine(nameof(RefillDash), 1);
		}

		if (LastOnWallLeftTime > 0 || LastOnWallRightTime > 0)
		{
			StartCoroutine(nameof(RefillDash), 1);
		}

		return _dashesLeft > 0;
	}

	public bool CanSlide()
	{
		if (LastOnWallTime > 0 && !IsJumping && !IsWallJumping && !IsDashing && LastOnGroundTime <= 0)
			return true;
		else
			return false;
	}
	#endregion


	#region EDITOR METHODS
	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(_groundCheckPoint.position, _groundCheckSize);
		Gizmos.color = Color.blue;
		Gizmos.DrawWireCube(_frontWallCheckPoint.position, _wallCheckSize);
		Gizmos.DrawWireCube(_backWallCheckPoint.position, _wallCheckSize);
	}
	#endregion
	
	private bool AnyJoystickButtonDown(params int[] buttons)
    {
        foreach (int b in buttons)
        {
            if (b < 0 || b > 19) continue;
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + b)))
                return true;
        }
        return false;
    }

    private bool AnyJoystickButtonUp(params int[] buttons)
    {
        foreach (int b in buttons)
        {
            if (b < 0 || b > 19) continue;
            if (Input.GetKeyUp((KeyCode)((int)KeyCode.JoystickButton0 + b)))
                return true;
        }
        return false;
    }
}

