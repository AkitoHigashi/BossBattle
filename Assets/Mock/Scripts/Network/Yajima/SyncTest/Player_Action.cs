using UnityEngine;

namespace NetworkTest_Yajima
{
    public partial class Player
    {
        /// <summary>
        /// 移動メソッド
        /// </summary>
        void Move()
        {
            if (!IsOwner) return;
            // 入力受け取り
            var direction = _inputActions.Player.Move.ReadValue<Vector2>() * 0.05f;

            // 移動
            var prePos = transform.position;
            prePos.x += direction.x;
            prePos.z += direction.y;
            transform.position = prePos;
        }
    }
}
