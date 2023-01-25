using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TK.Rendering.PostFX
{
    public sealed class FollowFocusManager : MonoBehaviour
    {
        private static AFDOFSettings settings;

        private static Transform m_target;
        public static Transform Target { get { return m_target; } set { m_target = value; } }

        private static Transform playerTarget;

        private void Awake()
        {
            settings = this.gameObject.GetComponent<AFDOFSettings>();
            if (settings == null)
                return;
            playerTarget = settings.depthOfFieldTarget;
        }

        public static void Change2Target()
        {
            settings.depthOfFieldTarget = m_target;
        }

        public static void ChangeDefault()
        {
            settings.depthOfFieldTarget = playerTarget;
        }
    }
}
