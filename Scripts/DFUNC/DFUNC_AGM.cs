﻿
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class DFUNC_AGM : UdonSharpBehaviour
{
    [SerializeField] public UdonSharpBehaviour SAVControl;
    [SerializeField] private Animator AGMAnimator;
    [Tooltip("Camera script that is used to see the target")]
    public GameObject AGM;
    public int NumAGM = 4;
    [SerializeField] private Text HUDText_AGM_ammo;
    [Tooltip("Camera that renders onto the AtGScreen")]
    [SerializeField] private Camera AtGCam;
    [Tooltip("Screen that displays target, that is enabled when selected")]
    [SerializeField] private GameObject AtGScreen;
    [SerializeField] private GameObject Dial_Funcon;
    [Tooltip("How long it takes to fully reload from empty in seconds. Can be inaccurate because it can only reload by integers per resupply")]
    [SerializeField] private float FullReloadTimeSec = 8;
    [SerializeField] private AudioSource AGMLock;
    [SerializeField] private AudioSource AGMUnlock;
    [SerializeField] private bool AllowFiringWhenGrounded;
    [SerializeField] private bool DoAnimBool = false;
    [SerializeField] private string AnimBoolName = "AGMSelected";
    [Tooltip("Should the boolean stay true if the pilot exits with it selected?")]
    [SerializeField] private bool AnimBoolStayTrueOnExit;
    [UdonSynced, FieldChangeCallback(nameof(AGMFire))] private short _AGMFire;
    public short AGMFire
    {
        set
        {
            _AGMFire = value;
            LaunchAGM();
        }
        get => _AGMFire;
    }
    private float boolToggleTime;
    private bool AnimOn = false;
    private int AnimBool_STRING;
    private SaccEntity EntityControl;
    private bool UseLeftTrigger = false;
    [System.NonSerializedAttribute] public bool AGMLocked;
    [System.NonSerializedAttribute] public bool IsOwner;
    private int AGMUnlocking = 0;
    private float AGMUnlockTimer;
    private float AGMRotDif;
    private bool TriggerLastFrame;
    private float TriggerTapTime;
    [System.NonSerializedAttribute] public int FullAGMs;
    public Transform AGMLaunchPoint;
    public LayerMask AGMTargetsLayer;
    private float FullAGMsDivider;
    [System.NonSerializedAttribute] [UdonSynced(UdonSyncMode.None)] public Vector3 AGMTarget;
    private VRCPlayerApi localPlayer;
    private bool InVR;
    private bool InEditor;
    private Transform VehicleTransform;
    private Quaternion AGMCamRotSlerper;
    private Quaternion AGMCamLastFrame;
    private bool func_active;
    private float reloadspeed;
    private int AGMLAUNCHED_STRING = Animator.StringToHash("agmlaunched");
    private int AGMS_STRING = Animator.StringToHash("AGMs");
    private bool LeftDial = false;
    private int DialPosition = -999;
    private bool OthersEnabled;
    private bool Piloting;
    public void DFUNC_LeftDial() { UseLeftTrigger = true; }
    public void DFUNC_RightDial() { UseLeftTrigger = false; }
    public void SFEXT_L_EntityStart()
    {
        FullAGMs = NumAGM;
        reloadspeed = FullAGMs / FullReloadTimeSec;
        FullAGMsDivider = 1f / (NumAGM > 0 ? NumAGM : 10000000);
        localPlayer = Networking.LocalPlayer;
        InEditor = localPlayer == null;
        EntityControl = (SaccEntity)SAVControl.GetProgramVariable("EntityControl");
        VehicleTransform = EntityControl.transform;
        AGMAnimator.SetFloat(AGMS_STRING, (float)NumAGM * FullAGMsDivider);
        if (Dial_Funcon) Dial_Funcon.SetActive(false);

        FindSelf();

        if (HUDText_AGM_ammo) { HUDText_AGM_ammo.text = NumAGM.ToString("F0"); }
        AnimBool_STRING = Animator.StringToHash(AnimBoolName);
    }
    public void SFEXT_O_PilotEnter()
    {
        TriggerLastFrame = true;
        AGMLocked = false;
        Piloting = true;
        if (!InEditor) { InVR = localPlayer.IsUserInVR(); }
        if (HUDText_AGM_ammo) { HUDText_AGM_ammo.text = NumAGM.ToString("F0"); }
    }
    public void SFEXT_P_PassengerEnter()
    {
        if (HUDText_AGM_ammo) { HUDText_AGM_ammo.text = NumAGM.ToString("F0"); }
    }
    public void SFEXT_G_PilotExit()
    {
        if (OthersEnabled) { DisableForOthers(); }
        if (DoAnimBool && !AnimBoolStayTrueOnExit && AnimOn)
        { SetBoolOff(); }
    }
    public void SFEXT_O_PilotExit()
    {
        AGMLocked = false;
        AtGScreen.SetActive(false);
        AtGCam.gameObject.SetActive(false);
        gameObject.SetActive(false);
        func_active = false;
        Piloting = false;
    }
    public void SFEXT_G_RespawnButton()
    {
        NumAGM = FullAGMs;
        AGMAnimator.SetFloat(AGMS_STRING, 1);
        if (DoAnimBool && AnimOn)
        { SetBoolOff(); }
    }
    public void SFEXT_G_ReSupply()
    {
        if (NumAGM != FullAGMs)
        { SAVControl.SetProgramVariable("ReSupplied", (int)SAVControl.GetProgramVariable("ReSupplied") + 1); }
        NumAGM = (int)Mathf.Min(NumAGM + Mathf.Max(Mathf.Floor(reloadspeed), 1), FullAGMs);
        AGMAnimator.SetFloat(AGMS_STRING, (float)NumAGM * FullAGMsDivider);
        if (HUDText_AGM_ammo) { HUDText_AGM_ammo.text = NumAGM.ToString("F0"); }
    }
    public void SFEXT_G_Explode()
    {
        NumAGM = FullAGMs;
        AGMAnimator.SetFloat(AGMS_STRING, 1);
        if (func_active)
        { DFUNC_Deselected(); }
        if (DoAnimBool && AnimOn)
        { SetBoolOff(); }
    }
    public void DFUNC_Selected()
    {
        func_active = true;
        gameObject.SetActive(true);
        if (DoAnimBool && !AnimOn)
        { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetBoolOn)); }
        if (!OthersEnabled) { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(EnableForOthers)); }
    }
    public void DFUNC_Deselected()
    {
        TriggerLastFrame = true;
        func_active = false;
        AtGScreen.SetActive(false);
        AtGCam.gameObject.SetActive(false);
        gameObject.SetActive(false);
        if (DoAnimBool && AnimOn)
        { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SetBoolOff)); }
        if (OthersEnabled) { SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(DisableForOthers)); }
    }
    public void EnableForOthers()
    {
        if (!Piloting)
        { gameObject.SetActive(true); }
        OthersEnabled = true;
    }
    public void DisableForOthers()
    {
        if (!Piloting)
        { gameObject.SetActive(false); }
        OthersEnabled = false;
    }
    private void Update()
    {
        if (func_active)
        {
            float DeltaTime = Time.deltaTime;
            TriggerTapTime += DeltaTime;
            AGMUnlockTimer += DeltaTime * AGMUnlocking;//AGMUnlocking is 1 if it was locked and just pressed, else 0, (waits for double tap delay to disable)
            float Trigger;
            if (UseLeftTrigger)
            { Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger"); }
            else
            { Trigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger"); }
            if (AGMUnlockTimer > 0.4f && AGMLocked == true)
            {
                //disable for others because they no longer need to sync
                AGMLocked = false;
                AGMUnlockTimer = 0;
                AGMUnlocking = 0;
                if (Dial_Funcon) { Dial_Funcon.SetActive(false); }
                if (AGMUnlock)
                { AGMUnlock.Play(); }
            }
            if (Trigger > 0.75 || (Input.GetKey(KeyCode.Space)))
            {
                if (!TriggerLastFrame)
                {//new button press
                    if (TriggerTapTime < 0.4f)
                    {//double tap detected
                        if (AGMLocked)
                        {//locked on, launch missile
                            if (NumAGM > 0 && (AllowFiringWhenGrounded || !(bool)SAVControl.GetProgramVariable("Taxiing")))
                            {
                                AGMFire++;//launch AGM using set
                                RequestSerialization();
                                if ((bool)SAVControl.GetProgramVariable("IsOwner"))
                                { EntityControl.SendEventToExtensions("SFEXT_O_AGMLaunch"); }
                            }
                            AGMUnlocking = 0;
                        }
                    }
                    else if (!AGMLocked)
                    {//lock onto a target
                        if (AtGCam)
                        {
                            //check for agmtargets to lock to
                            float targetangle = 999;
                            RaycastHit lockpoint;
                            RaycastHit[] agmtargs = Physics.SphereCastAll(AtGCam.transform.position, 150, AtGCam.transform.forward, Mathf.Infinity, AGMTargetsLayer);
                            if (agmtargs.Length > 0)
                            {//found one or more, find lowest angle one
                                //find target with lowest angle from crosshair
                                foreach (RaycastHit target in agmtargs)
                                {
                                    Vector3 targetdirection = target.point - AtGCam.transform.position;
                                    float angle = Vector3.Angle(AtGCam.transform.forward, targetdirection);
                                    if (angle < targetangle)
                                    {
                                        //enable for others so they sync the variable
                                        targetangle = angle;
                                        AGMTarget = target.collider.transform.position;
                                        AGMLocked = true;
                                        AGMUnlocking = 0;
                                        RequestSerialization();
                                        if (Dial_Funcon) { Dial_Funcon.SetActive(true); }
                                    }
                                }
                            }
                            else
                            {//didn't find one, lock onto raycast point
                                Physics.Raycast(AtGCam.transform.position, AtGCam.transform.forward, out lockpoint, Mathf.Infinity, 133121 /* Default, Environment, and Walkthrough */, QueryTriggerInteraction.Ignore);
                                //enable for others so they sync the variable
                                if (AGMLock)
                                { AGMLock.Play(); }
                                AGMTarget = lockpoint.point;
                                AGMLocked = true;
                                AGMUnlocking = 0;
                                RequestSerialization();
                                if (Dial_Funcon) { Dial_Funcon.SetActive(true); }
                            }
                        }
                    }
                    else
                    {
                        TriggerTapTime = 0;
                        AGMUnlockTimer = 0;
                        AGMUnlocking = 1;
                    }
                }
                TriggerLastFrame = true;
            }
            else { TriggerLastFrame = false; }
            if (!AGMLocked)
            {
                Quaternion newangle;
                if (InVR)
                {
                    if (UseLeftTrigger)
                    { newangle = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation * Quaternion.Euler(0, 60, 0); }
                    else
                    { newangle = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation * Quaternion.Euler(0, 60, 0); }
                }
                else if (!InEditor)//desktop mode
                {
                    newangle = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                }
                else//editor
                {
                    newangle = VehicleTransform.rotation;
                }
                float ZoomLevel = AtGCam.fieldOfView / 90;
                AGMCamRotSlerper = Quaternion.Slerp(AGMCamRotSlerper, newangle, ZoomLevel * 220f * DeltaTime);

                if (AtGCam)
                {
                    AGMRotDif = Vector3.Angle(AtGCam.transform.rotation * Vector3.forward, AGMCamLastFrame * Vector3.forward);
                    // AGMRotDif = Vector3.Angle(AtGCam.transform.rotation * Vector3.forward, AGMCamRotSlerper * Vector3.forward);
                    AtGCam.transform.rotation = AGMCamRotSlerper;

                    Vector3 temp2 = AtGCam.transform.localRotation.eulerAngles;
                    temp2.z = 0;
                    AtGCam.transform.localRotation = Quaternion.Euler(temp2);
                }
                AGMCamLastFrame = newangle;
            }


            //AGMScreen
            float SmoothDeltaTime = Time.smoothDeltaTime;
            if (!AGMLocked)
            {
                AtGScreen.SetActive(true);
                AtGCam.gameObject.SetActive(true);
                //if turning camera fast, zoom out
                if (AGMRotDif < 2.5f)
                {
                    RaycastHit camhit;
                    Physics.Raycast(AtGCam.transform.position, AtGCam.transform.forward, out camhit, Mathf.Infinity, 1);
                    //dolly zoom //Mathf.Atan(100 <--the 100 is the height of the camera frustrum at the target distance
                    float newzoom = Mathf.Clamp(2.0f * Mathf.Atan(100 * 0.5f / Vector3.Distance(gameObject.transform.position, camhit.point)) * Mathf.Rad2Deg, 1.5f, 90);
                    AtGCam.fieldOfView = Mathf.Clamp(Mathf.Lerp(AtGCam.fieldOfView, newzoom, 1.5f * SmoothDeltaTime), 0.3f, 90);
                }
                else
                {
                    float newzoom = 80;
                    AtGCam.fieldOfView = Mathf.Clamp(Mathf.Lerp(AtGCam.fieldOfView, newzoom, 3.5f * SmoothDeltaTime), 0.3f, 90); //zooming in is a bit slower than zooming out                       
                }
            }
            else
            {
                AtGScreen.SetActive(true);
                AtGCam.gameObject.SetActive(true);
                AtGCam.transform.LookAt(AGMTarget, EntityControl.transform.up);

                RaycastHit camhit;
                Physics.Raycast(AtGCam.transform.position, AtGCam.transform.forward, out camhit, Mathf.Infinity, 1);
                //dolly zoom //Mathf.Atan(40 <--the 40 is the height of the camera frustrum at the target distance
                AtGCam.fieldOfView = Mathf.Max(Mathf.Lerp(AtGCam.fieldOfView, 2.0f * Mathf.Atan(60 * 0.5f / Vector3.Distance(gameObject.transform.position, camhit.point)) * Mathf.Rad2Deg, 5 * SmoothDeltaTime), 0.3f);
            }
        }
    }

    public void LaunchAGM()
    {
        if (NumAGM > 0) { NumAGM--; }
        if (!InEditor) { IsOwner = localPlayer.IsOwner(gameObject); } else { IsOwner = true; }
        AGMAnimator.SetTrigger(AGMLAUNCHED_STRING);
        if (AGM)
        {
            GameObject NewAGM = Object.Instantiate(AGM);
            NewAGM.transform.SetPositionAndRotation(AGMLaunchPoint.position, AGMLaunchPoint.rotation);
            NewAGM.SetActive(true);
            NewAGM.GetComponent<Rigidbody>().velocity = (Vector3)SAVControl.GetProgramVariable("CurrentVel");
        }
        AGMAnimator.SetFloat(AGMS_STRING, (float)NumAGM * FullAGMsDivider);
        if (HUDText_AGM_ammo) { HUDText_AGM_ammo.text = NumAGM.ToString("F0"); }
    }
    private void FindSelf()
    {
        int x = 0;
        foreach (UdonSharpBehaviour usb in EntityControl.Dial_Functions_R)
        {
            if (this == usb)
            {
                DialPosition = x;
                return;
            }
            x++;
        }
        LeftDial = true;
        x = 0;
        foreach (UdonSharpBehaviour usb in EntityControl.Dial_Functions_L)
        {
            if (this == usb)
            {
                DialPosition = x;
                return;
            }
            x++;
        }
        DialPosition = -999;
        Debug.LogWarning("DFUNC_AGM: Can't find self in dial functions");
    }
    public void SetBoolOn()
    {
        boolToggleTime = Time.time;
        AnimOn = true;
        AGMAnimator.SetBool(AnimBool_STRING, AnimOn);
    }
    public void SetBoolOff()
    {
        boolToggleTime = Time.time;
        AnimOn = false;
        AGMAnimator.SetBool(AnimBool_STRING, AnimOn);
    }
    public void KeyboardInput()
    {
        if (LeftDial)
        {
            if (EntityControl.LStickSelection == DialPosition)
            { EntityControl.LStickSelection = -1; }
            else
            { EntityControl.LStickSelection = DialPosition; }
        }
        else
        {
            if (EntityControl.RStickSelection == DialPosition)
            { EntityControl.RStickSelection = -1; }
            else
            { EntityControl.RStickSelection = DialPosition; }
        }
    }
}
