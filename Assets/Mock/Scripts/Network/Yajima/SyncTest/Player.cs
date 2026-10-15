using FishNet.Object;
using UnityEngine;

namespace NetworkTest_Yajima
{
    /// <summary>
    /// <para>簡易プレイヤー</para>
    /// <para>移動のみ</para>
    /// </summary>
    public partial class Player : NetworkBehaviour
    {
        InputSystem_Actions _inputActions;
        bool _registered = false;

        public void InitOwner()
        {
            if (!IsOwner) return;
            _inputActions ??= new();
            RegisterAction();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            InitOwner();
        }

        public override void OnStopClient()
        {
            UnregisterAction();
            base.OnStopClient();
        }

        private void OnDestroy()
        {
            UnregisterAction();
            _inputActions?.Dispose();
        }

        private void OnEnable()
        {
            RegisterAction();
        }

        private void OnDisable()
        {
            UnregisterAction();
        }

        private void Update()
        {
            Move();
        }

        /// <summary>
        /// アクション登録メソッド
        /// </summary>
        void RegisterAction()
        {
            if (!IsOwner || _inputActions == null) return;
            if (_registered) return;

            _inputActions.Enable();
            _registered = true;
            Debug.Log("Action Registered");
        }

        /// <summary>
        /// アクション解除メソッド
        /// </summary>
        void UnregisterAction()
        {
            if (_inputActions == null) return;
            if (!_registered) return;

            _inputActions.Disable();
            _registered = false;
            Debug.Log("Action Unregistered");
        }
    }
}
