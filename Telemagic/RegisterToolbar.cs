﻿using System;
using UnityEngine;
using ToolbarControl_NS;

namespace Telemagic
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start() {
            ToolbarControl.RegisterMod("Telemagic", "Telemagic");
        }
    }
}
