﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;

/*
Source code copyrighgt 2017, by Michael Billard (Angel-125)
License: GNU General Public License Version 3
License URL: http://www.gnu.org/licenses/
If you want to use this code, give me a shout on the KSP forums! :)
Wild Blue Industries is trademarked by Michael Billard and may be used for non-commercial purposes. All other rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
namespace KerbalActuators
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class WBIVTOLManager : MonoBehaviour
    {
        public const string kEngineGroup = "Engine";
        public const string LABEL_HOVER = "HOVR";
        public const string LABEL_VSPDINC = "VSPD +";
        public const string LABEL_VSPDZERO = "VSPD 0";
        public const string LABEL_VSPDDEC = "VSPD -";

        public static WBIVTOLManager Instance;

        public KeyCode codeToggleHover = KeyCode.Insert;
        public KeyCode codeIncreaseVSpeed = KeyCode.PageUp;
        public KeyCode codeDecreaseVSpeed = KeyCode.PageDown;
        public KeyCode codeZeroVSpeed = KeyCode.Delete;

        public bool hoverActive = false;
        public float verticalSpeed = 0f;
        public float verticalSpeedIncrements = 1f;
        public Vessel vessel;
        public Dictionary<string, KeyCode> controlCodes = new Dictionary<string,KeyCode>();

        private IAirParkController airParkController;
        private IHoverController[] hoverControllers;
        private IRotationController[] rotationControllers;
        private IPropSpinner[] propSpinners;
        private HoverVTOLGUI hoverGUI = new HoverVTOLGUI();
        private string hoverControlsPath;

        public void Start()
        {
            WBIVTOLManager.Instance = this;
            GameEvents.onVesselLoaded.Add(VesselWasLoaded);
            GameEvents.onVesselChange.Add(VesselWasChanged);
            GameEvents.onStageActivate.Add(OnStageActivate);

            hoverGUI.vtolManager = this;
            hoverGUI.hoverSetupGUI.vtolManager = this;

            this.vessel = FlightGlobals.ActiveVessel;

            //Get the current control code mappings
            hoverControlsPath = AssemblyLoader.loadedAssemblies.GetPathByType(typeof(WBIVTOLManager)) + "/VTOLControls.cfg";
            LoadControls();
        }

        public void LoadControls()
        {
            ConfigNode nodeControls = null;
            KeyCode keyCode;

            //Now load the controls
            nodeControls = ConfigNode.Load(hoverControlsPath);
            if (nodeControls != null)
            {
                if (nodeControls.HasValue(LABEL_HOVER))
                {
                    keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), nodeControls.GetValue(LABEL_HOVER));
                    controlCodes.Add(LABEL_HOVER, keyCode);
                    codeToggleHover = keyCode;
                }
                else
                {
                    controlCodes.Add(LABEL_HOVER, KeyCode.Insert);
                }

                if (nodeControls.HasValue(LABEL_VSPDINC))
                {
                    keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), nodeControls.GetValue(LABEL_VSPDINC));
                    controlCodes.Add(LABEL_VSPDINC, keyCode);
                    codeIncreaseVSpeed = keyCode;
                }
                else
                {
                    controlCodes.Add(LABEL_VSPDINC, KeyCode.PageUp);
                }

                if (nodeControls.HasValue(LABEL_VSPDZERO))
                {
                    keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), nodeControls.GetValue(LABEL_VSPDZERO));
                    controlCodes.Add(LABEL_VSPDZERO, keyCode);
                    codeZeroVSpeed = keyCode;
                }
                else
                {
                    controlCodes.Add(LABEL_VSPDZERO, KeyCode.Delete);
                }

                if (nodeControls.HasValue(LABEL_VSPDDEC))
                {
                    keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), nodeControls.GetValue(LABEL_VSPDDEC));
                    controlCodes.Add(LABEL_VSPDDEC, keyCode);
                    codeDecreaseVSpeed = keyCode;
                }
                else
                {
                    controlCodes.Add(LABEL_VSPDDEC, KeyCode.PageDown);
                }

            }

            //Set default values
            else
            {
                controlCodes.Clear();
                controlCodes.Add(LABEL_HOVER, KeyCode.Insert);
                controlCodes.Add(LABEL_VSPDINC, KeyCode.PageUp);
                controlCodes.Add(LABEL_VSPDZERO, KeyCode.Delete);
                controlCodes.Add(LABEL_VSPDDEC, KeyCode.PageDown);
            }
        }

        protected void saveControls()
        {
            ConfigNode nodeControls = new ConfigNode();

            nodeControls.name = "VTOL_CONTROLS";

            nodeControls.AddValue(LABEL_HOVER, controlCodes[LABEL_HOVER]);
            nodeControls.AddValue(LABEL_VSPDINC, controlCodes[LABEL_VSPDINC]);
            nodeControls.AddValue(LABEL_VSPDZERO, controlCodes[LABEL_VSPDZERO]);
            nodeControls.AddValue(LABEL_VSPDDEC, controlCodes[LABEL_VSPDDEC]);

            nodeControls.Save(hoverControlsPath);
        }

        public virtual void SetControlCodes(Dictionary<string, KeyCode> newCodes)
        {
            controlCodes = newCodes;

            foreach (string key in newCodes.Keys)
            {
                switch (key)
                {
                    case LABEL_HOVER:
                        codeToggleHover = newCodes[key];
                        break;

                    case LABEL_VSPDINC:
                        codeIncreaseVSpeed = newCodes[key];
                        break;

                    case LABEL_VSPDZERO:
                        codeZeroVSpeed = newCodes[key];
                        break;

                    case LABEL_VSPDDEC:
                        codeDecreaseVSpeed = newCodes[key];
                        break;
                }
            }

            saveControls();
        }

        public void OnStageActivate(int stageID)
        {
            hoverGUI.enginesActive = EnginesAreActive();
        }

        public void VesselWasChanged(Vessel vessel)
        {
            FindControllers(vessel);
        }

        public void VesselWasLoaded(Vessel vessel)
        {
            FindControllers(vessel);
        }

        public void FindControllers(Vessel vessel)
        {
            this.vessel = vessel;
            FindHoverControllers();
            FindRotationControllers();
            FindPropSpinners();
            FindAirParkControllers();
        }

        public void FindAirParkControllers()
        {
            airParkController = vessel.FindPartModuleImplementing<IAirParkController>();
        }

        public void FindHoverControllers()
        {
            List<IHoverController> controllers = vessel.FindPartModulesImplementing<IHoverController>();

            if (controllers.Count > 0)
                hoverControllers = controllers.ToArray();
        }

        public void FindRotationControllers()
        {
            List<IRotationController> controllers = vessel.FindPartModulesImplementing<IRotationController>();
            List<IRotationController> rotationControllerList = new List<IRotationController>();

            //Find all the controllers that belong to the Engine list.
            IRotationController[] rotationItems = controllers.ToArray();
            for (int index = 0; index < rotationItems.Length; index++)
            {
                if (rotationItems[index].GetGroupID() == kEngineGroup)
                    rotationControllerList.Add(rotationItems[index]);
            }

            if (rotationControllerList.Count > 0)
                rotationControllers = rotationControllerList.ToArray();
        }

        public bool IsParked()
        {
            if (airParkController == null)
                return true;

            return airParkController.IsParked();
        }

        public void TogglePark()
        {
            if (airParkController != null)
                airParkController.TogglePark();
        }

        public string GetSituation()
        {
            if (airParkController != null)
                return airParkController.GetSituation();
            else
                return "N/A";
        }

        public void SetPark(bool isParked)
        {
            if (airParkController != null)
            {
                airParkController.SetParking(isParked);
            }
        }

        public void FindPropSpinners()
        {
            List<IPropSpinner> spinners = vessel.FindPartModulesImplementing<IPropSpinner>();

            if (spinners.Count > 0)
                propSpinners = spinners.ToArray();
        }

        public bool CanRotateMin()
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return false;

            for (int index = 0; index < rotationControllers.Length; index++)
            {
                if (rotationControllers[index].CanRotateMin() == false)
                    return false;
            }

            return true;
        }

        public bool CanRotateMax()
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return false;

            for (int index = 0; index < rotationControllers.Length; index++)
            {
                if (rotationControllers[index].CanRotateMax() == false)
                    return false;
            }

            return true;
        }

        public void RotateToMin()
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return;

            for (int index = 0; index < rotationControllers.Length; index++)
                rotationControllers[index].RotateMin(false);
        }

        public void RotateToMax()
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return;

            for (int index = 0; index < rotationControllers.Length; index++)
                rotationControllers[index].RotateMax(false);
        }

        public void RotateToNeutral()
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return;

            for (int index = 0; index < rotationControllers.Length; index++)
                rotationControllers[index].RotateNeutral(false);
        }

        public void IncreaseRotationAngle(float rotationDelta)
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return;

            for (int index = 0; index < rotationControllers.Length; index++)
                rotationControllers[index].RotateUp(rotationDelta);
        }

        public void DecreaseRotationAngle(float rotationDelta)
        {
            if (rotationControllers == null || rotationControllers.Length == 0)
                return;

            for (int index = 0; index < rotationControllers.Length; index++)
                rotationControllers[index].RotateDown(rotationDelta);
        }

        public void ToggleThrust()
        {
            if (propSpinners == null || propSpinners.Length == 0)
                return;

            for (int index = 0; index < propSpinners.Length; index++)
            {
                propSpinners[index].ToggleThrust();
            }
        }

        public void SetForwardThrust()
        {
            if (propSpinners == null || propSpinners.Length == 0)
                return;

            for (int index = 0; index < propSpinners.Length; index++)
                propSpinners[index].SetReverseThrust(false);
        }

        public void SetReverseThrust()
        {
            if (propSpinners == null || propSpinners.Length == 0)
                return;

            for (int index = 0; index < propSpinners.Length; index++)
            {
                propSpinners[index].SetReverseThrust(true);
            }
        }

        public bool EnginesAreActive()
        {
            if (hoverControllers == null || hoverControllers.Length == 0)
                return false;

            for (int index = 0; index < hoverControllers.Length; index++)
            {
                if (hoverControllers[index].IsEngineActive() == false)
                    return false;
            }

            return true;
        }

        public void StartEngines()
        {
            if (hoverControllers == null || hoverControllers.Length == 0)
                return;

            for (int index = 0; index < hoverControllers.Length; index++)
                hoverControllers[index].StartEngine();
        }

        public void StopEngines()
        {
            if (hoverControllers == null || hoverControllers.Length == 0)
                return;

            if (hoverActive)
                ToggleHover();

            hoverGUI.enginesActive = false;

            for (int index = 0; index < hoverControllers.Length; index++)
                hoverControllers[index].StopEngine();
        }

        public void DecreaseVerticalSpeed(float amount = 1.0f)
        {
            if (hoverControllers.Length == 0)
                return;
            if (hoverActive == false)
                ToggleHover();

            verticalSpeed -= amount;

            for (int index = 0; index < hoverControllers.Length; index++)
                hoverControllers[index].SetVerticalSpeed(verticalSpeed);
        }

        public void IncreaseVerticalSpeed(float amount = 1.0f)
        {
            if (hoverControllers.Length == 0)
                return;
            if (hoverActive == false)
                ToggleHover();

            verticalSpeed += amount;

            for (int index = 0; index < hoverControllers.Length; index++)
                hoverControllers[index].SetVerticalSpeed(verticalSpeed);
        }

        public void KillVerticalSpeed()
        {
            if (hoverControllers.Length == 0)
                return;
            if (hoverActive == false)
                ToggleHover();

            verticalSpeed = 0f;

            for (int index = 0; index < hoverControllers.Length; index++)
                hoverControllers[index].KillVerticalSpeed();
        }

        public void ToggleHover()
        {
            if (hoverControllers.Length == 0)
                return;
            hoverActive = !hoverActive;
            if (!hoverActive)
                verticalSpeed = 0f;

            //Set hover mode
            //We actually DON'T want to calculate the throttle setting because other engines that aren't in hover mode might need it.
            for (int index = 0; index < hoverControllers.Length; index++)
            {
                hoverControllers[index].SetHoverMode(hoverActive);
            }
        }

        public void ToggleGUI()
        {
            if (!hoverGUI.IsVisible())
            {
                WBIActuatorsGUIMgr.Instance.RegisterWindow(hoverGUI);
                WBIActuatorsGUIMgr.Instance.RegisterWindow(hoverGUI.hoverSetupGUI);
                ShowGUI();
            }
            else
            {
                WBIActuatorsGUIMgr.Instance.UnregisterWindow(hoverGUI);
                WBIActuatorsGUIMgr.Instance.UnregisterWindow(hoverGUI.hoverSetupGUI);
                hoverGUI.SetVisible(false);
            }
        }

        public void ShowGUI()
        {
            FindControllers(FlightGlobals.ActiveVessel);

            if (airParkController != null)
                hoverGUI.canDrawParkingControls = true;
            else
                hoverGUI.canDrawParkingControls = false;

            if (hoverControllers != null && hoverControllers.Length > 0)
                hoverGUI.canDrawHoverControls = true;
            else
                hoverGUI.canDrawHoverControls = false;

            if (rotationControllers != null && rotationControllers.Length > 0)
                hoverGUI.canDrawRotationControls = true;
            else
                hoverGUI.canDrawRotationControls = false;

            if (propSpinners != null && propSpinners.Length > 0)
                hoverGUI.canDrawThrustControls = true;
            else
                hoverGUI.canDrawThrustControls = false;

            hoverGUI.enginesActive = EnginesAreActive();
            hoverGUI.canRotateMax = CanRotateMax();
            hoverGUI.canRotateMin = CanRotateMin();

            hoverGUI.SetVisible(true);
        }

        public void Update()
        {
            if (Input.GetKeyDown(codeDecreaseVSpeed))
            {
                DecreaseVerticalSpeed();
                printSpeed();
            }

            if (Input.GetKeyDown(codeIncreaseVSpeed))
            {
                IncreaseVerticalSpeed();
                printSpeed();
            }

            if (Input.GetKeyDown(codeZeroVSpeed))
            {
                KillVerticalSpeed();
                printSpeed();
            }

            if (Input.GetKeyDown(codeToggleHover))
            {
                ToggleHover();

                if (hoverActive)
                    ScreenMessages.PostScreenMessage(new ScreenMessage("Hover ON", 1f, ScreenMessageStyle.UPPER_CENTER));
                else
                    ScreenMessages.PostScreenMessage(new ScreenMessage("Hover OFF", 1f, ScreenMessageStyle.UPPER_CENTER));
            }
        }

        public void FixedUpdate()
        {
            if (hoverControllers == null)
                return;
            if (hoverControllers.Length == 0)
                return;
            if (!hoverActive)
                return;

            //This is crude but effective. What we do is jitter the engine throttle up and down to maintain desired vertical speed.
            //It tends to vibrate the engines but they're ok. This will have to do until I can figure out the relation between
            //engine.finalThrust, engine.maxThrust, and the force needed to make the craft hover.
            float throttleState = 0;
            if (FlightGlobals.ActiveVessel.verticalSpeed >= verticalSpeed)
                throttleState = 0f;
            else
                throttleState = 1.0f;

            for (int index = 0; index < hoverControllers.Length; index++)
            {
                hoverControllers[index].UpdateHoverState(throttleState);
            }
        }

        public virtual void printSpeed()
        {
            ScreenMessages.PostScreenMessage(new ScreenMessage("Hover Climb Rate: " + verticalSpeed, 1f, ScreenMessageStyle.UPPER_CENTER));
        }

        public string LabelForKeyCode(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.PageDown:
                    return "PgDn";

                case KeyCode.PageUp:
                    return "PgUp";

                case KeyCode.Delete:
                    return "Del";

                default:
                    return code.ToString();
            }
        }
    }
}
