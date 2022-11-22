using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace kspFSD
{

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class fsdSupercruise : PartModule
    {
        const int MLC = -1;
        const int DRP = 3;
        const int OC = 5;
        const int SC = 10;

        public static bool SUPERCRUISE = false;
        public static bool DROPPING = false;
        private double supercruiseTargetVel = 0.0d;

        double fsdSpeedMin = 0;
        double fsdSpeedMax = 0;
        public static double currentVel = 2500d;

        [KSPEvent(groupName = "FSDControls", guiActive = true, active = true, guiActiveEditor = false, guiName = "Toggle Supercruise", guiActiveUnfocused = false)]

        public void StopVessel()
        {
            DROPPING = true;
            SUPERCRUISE = false;
        }
        public void toggleSupercruise()
        {

            if (getState()!=MLC)//û����������ʱ
            {
                if (SUPERCRUISE)//�ղ����ڳ�Ѳ��׼���˳���Ѳ
                {
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null) //�����ǰ�зɴ�Ŀ��
                    {
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km��Ŀ������
                        {
                            ScreenMessages.PostScreenMessage("Destination Lock Engaged");
                            vessel.SetPosition(vessel.targetObject.GetTransform().position + new Vector3(2000, 2000, 2000));//λ�÷���5km��
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(-FlightGlobals.ActiveVessel.GetObtVelocity());
                            FlightGlobals.ActiveVessel.ChangeWorldVelocity(vessel.targetObject.GetVessel().GetObtVelocity());  //�ٶ�ͬ��
                        }
                    }
                    else//û�зɴ�Ŀ��
                    {
                        ScreenMessages.PostScreenMessage("Dropping From Supercruise");
                        //FlightGlobals.ActiveVessel.ChangeWorldVelocity((vesselOrientation * new Vector3(0.0f, (float)previousVelocity, 0.0f)) - flightVector);//ԭ������
                        StopVessel();
                        return;//��ǰ������ͣ����������л���Ѳ״̬
                    }
                    SetParts(false);
                }
                else
                {//�ղ�û�г�Ѳ����˼������볬Ѳ
                    ScreenMessages.PostScreenMessage("Supercruise Engaged");
                    SetParts(true);
                    currentVel = vessel.GetObtVelocity().magnitude;
                }
                SUPERCRUISE = !SUPERCRUISE;//����л�״̬
            }
            else
            {
                ScreenMessages.PostScreenMessage("Frame Shift Cancled: Mass Lock");
            }
        }

        [KSPAction(guiName = "Toggle Supercruise", isPersistent = true)]
        public void toggleSupercruiseAction(KSPActionParam param)
        {
            toggleSupercruise();
        }

        int getState()
        {
            CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
            double radius =mainBody.Radius;
            double minOrbitalALT = mainBody.minOrbitalDistance;
            double altitute = vessel.altitude;
            double radaraltitute = vessel.radarAltitude;
            double airpressure;
            if (mainBody.atmosphereDepth>0)
                airpressure = mainBody.GetPressureAtm(altitute);
            else
                airpressure = -1;

            if (radaraltitute <= 1000 || airpressure > 0.05)//��������MLC
            {
                return MLC;
            }
            else if (altitute < minOrbitalALT || airpressure > 0.01)//����߶�DRP
            {
                return DRP;
            }
            else if (altitute < minOrbitalALT + radius)//������и߶�OC
            {
                return OC;
            }
            else//��̫��SC
                return SC;
        }

        public void SetParts(bool b)
        {
            List<Part> partsList = FlightGlobals.ActiveVessel.parts;
            foreach (Part part in partsList)
            {
                if (part.attachJoint != null)
                {
                    part.attachJoint.SetUnbreakable(b, b);
                }
            }
        }
        public void FixedUpdate()
        {
            try
            {
                Quaternion vesselOrientation = FlightGlobals.ActiveVessel.GetTransform().rotation;
                float throttleLevel = FlightInputHandler.state.mainThrottle;
                CelestialBody mainBody = FlightGlobals.ActiveVessel.mainBody;
                double radius = mainBody.Radius;
                double minOrbitalALT = mainBody.minOrbitalDistance;
                double altitute = vessel.altitude;
                double radaraltitute = vessel.radarAltitude;
                int STATE = getState();

                double currentSpeed;
                if (SUPERCRUISE)
                {
                    //=================
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null)
                        if (Vector3.Distance(vessel.GetTransform().position, vessel.targetObject.GetTransform().position) < 100000d)//100km��Ŀ������
                            ScreenMessages.PostScreenMessage("Ready for Disengage");
                    //===================

                    switch (STATE)//fsd�ٶ�����������������
                    {
                        case SC:
                            {

                                break;
                            }
                        case OC:
                            {//��������ٶ�����������
                                ScreenMessages.PostScreenMessage("Orbital Flight Engaged");
                                break;
                            }
                        case DRP:
                            {//Glide�ٶ�����������
                                ScreenMessages.PostScreenMessage("Glide Engaged, Dropping From Supercruising");
                                break;
                            }
                        case MLC:
                            {//������������������
                                if (vessel.GetObtVelocity().magnitude > 1000)
                                    ScreenMessages.PostScreenMessage("Emergency Drop: Too Close");
                                StopVessel();
                                break;
                            }
                        default:
                            {
                                break;
                            }

                    }
                //���÷ɴ��ٶ�
                if (FlightGlobals.ActiveVessel == vessel)
                {
                    FlightGlobals.ActiveVessel.IgnoreGForces(1);
                    vessel.ChangeWorldVelocity((vesselOrientation * new Vector3(0.0f, (float)currentVel, 0.0f)) - vessel.GetObtVelocity());
                }
                if (!PauseMenu.isOpen)
                {
                    TimeWarp.SetRate(0, true, false);
                }
               }
                else if(DROPPING)
                {
                    {
                        //������ѷɴ����ٵ���
                    }
                    if (vessel.GetObtVelocity().magnitude < 1)
                    {
                        vessel.ChangeWorldVelocity(new Vector3d(0, 0, 0));
                        DROPPING = false;
                    }
                }
                else//�ڳ���ռ���Է���
                {

                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
    public class fsdHyperspaceJump : PartModule
    {
        [KSPEvent(groupName = "FSDControls", guiActive = true, active = true, guiActiveEditor = false, guiName = "Hyperspace Jump", guiActiveUnfocused = false)]
        public void hyperspaceJump()
        {
            try
            {
                CelestialBody targetDestination = FlightGlobals.ActiveVessel.patchedConicSolver.targetBody;
                CelestialBody gravityWell = FlightGlobals.ActiveVessel.mainBody;
                if (targetDestination != null && (FlightGlobals.ActiveVessel.altitude > (gravityWell.minOrbitalDistance - gravityWell.Radius + 2500.0d)))
                {
                    Vector3 movementVector = FlightGlobals.ActiveVessel.GetObtVelocity();
                    List<Part> partsList = FlightGlobals.ActiveVessel.parts;
                    foreach (Part part in partsList)
                    {
                        if (part.attachJoint != null)
                        {
                            part.attachJoint.SetUnbreakable(true, true);
                        }
                    }
                    FlightGlobals.ActiveVessel.ChangeWorldVelocity(-movementVector);
                    Orbit deployOrbit = new Orbit(0, 0, targetDestination.Radius * 2, 0, 0, 0, 0, targetDestination);
                    Vector3 deployPosition = deployOrbit.getPositionAtUT(Planetarium.GetUniversalTime());
                    OrbitPhysicsManager.HoldVesselUnpack(60);
                    FlightGlobals.ActiveVessel.IgnoreGForces(10);
                    FlightGlobals.ActiveVessel.IgnoreSpeed(10);
                    FlightGlobals.ActiveVessel.SetPosition(deployPosition);
                    FlightGlobals.ActiveVessel.ChangeWorldVelocity(-movementVector);
                    fsdSupercruise.SUPERCRUISE = true;
                    fsdSupercruise.currentVel = 2500d;
                    foreach (Part part in partsList)
                    {
                        if (part.attachJoint != null)
                        {
                            part.attachJoint.SetUnbreakable(false, false);
                        }
                    }
                    FlightGlobals.ActiveVessel.rootPart.AimCamera();
                    FloatingOrigin.ResetTerrainShaderOffset();
                    FloatingOrigin.SetOffset(FlightGlobals.ActiveVessel.GetWorldPos3D());
                }
            }
            catch (Exception ex)
            { Debug.Log(ex); }

        }
        [KSPAction(guiName = "Hyperspace Jump", isPersistent = true)]
        public void performJump(KSPActionParam param)
        {
            hyperspaceJump();
        }
    }
}