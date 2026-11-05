using UnityEngine;

namespace NetworkTest_Yajima
{
    public partial class Player
    {
        Rigidbody _rb;
        Vector2 _direction;
        bool _isJump;

        /// <summary>
        /// プレイヤーの移動入力を読み取り、_direction を更新する。
        /// </summary>
        /// <remarks>Player.Move アクションから Vector2 を取得し、0.05f のスケーリングを適用する。</remarks>
        void MoveInput()
        {
            // 入力受け取り
            _direction = _inputActions.Player.Move.ReadValue<Vector2>() * 0.05f;
        }

        /// <summary>
        /// 移動メソッド
        /// </summary>
        void Move()
        {
            _rb ??= GetComponent<Rigidbody>();
            if (_rb == null) return;

            // y方向の速度保存
            var yVelo = _rb.linearVelocity.y;
            // 速度更新
            _rb.linearVelocity = new Vector3(_direction.x * 20, yVelo, _direction.y * 20);
        }

        /// <summary>
        /// ジャンプ用入力メソッド
        /// </summary>
        void JumpInput()
        {
            // 入力受け取り
            _isJump = _inputActions.Player.Jump.WasPressedThisFrame();
        }

        void Jump()
        {
            if (!_isJump) return;

            _rb ??= GetComponent<Rigidbody>();
            if (_rb == null) return;
            _rb.AddForce(Vector3.up * 10, ForceMode.Impulse);
        }
    }
}
