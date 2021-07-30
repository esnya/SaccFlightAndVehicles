
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class PilotSeat : UdonSharpBehaviour
{
    public EngineController EngineControl;
    public GameObject SeatAdjuster;
    public GameObject PilotOnly;
    private int ThisStationID;
    private bool SeatInitialized = false;
    private Transform Seat;
    private Quaternion SeatStartRot;
    private Transform[] PilotOnlyScripts;
    private VRCPlayerApi localPlayer;
    private void Start()
    {
        Assert(EngineControl != null, "Start: EngineControl != null");
        Assert(PilotOnly != null, "Start: PilotOnly != null");
        Assert(SeatAdjuster != null, "Start: SeatAdjuster != null");

        localPlayer = Networking.LocalPlayer;

        Seat = ((VRC.SDK3.Components.VRCStation)GetComponent(typeof(VRC.SDK3.Components.VRCStation))).stationEnterPlayerLocation.transform;
        SeatStartRot = Seat.localRotation;

        if (PilotOnly != null)
        {
            Transform[] PO = PilotOnly.GetComponentsInChildren<Transform>();
            int i = 0;
            foreach (Transform obj in PO)
            {
                if ((UdonBehaviour)obj.GetComponent(typeof(UdonBehaviour)) != null)
                { i++; }
            }
            PilotOnlyScripts = new Transform[i];
            i = 0;
            foreach (Transform obj in PO)
            {
                if ((UdonBehaviour)obj.GetComponent(typeof(UdonBehaviour)) != null)
                {
                    PilotOnlyScripts[i] = obj;
                    i++;
                }
            }
        }
    }
    private void Interact()//entering the plane
    {
        if (!SeatInitialized) { InitializeSeat(); }
        EngineControl.MySeat = ThisStationID;

        EngineControl.PilotEnterPlaneLocal();

        Seat.rotation = Quaternion.Euler(0, Seat.eulerAngles.y, 0);//fixes offset seated position when getting in a rolled/pitched vehicle
        localPlayer.UseAttachedStation();
        Seat.localRotation = SeatStartRot;

        if (PilotOnly != null) { PilotOnly.SetActive(true); }
        if (SeatAdjuster != null) { SeatAdjuster.SetActive(true); }
    }
    public override void OnStationEntered(VRCPlayerApi player)
    {
        if (!SeatInitialized) { InitializeSeat(); }//can't do this in start because EngineControl might not have initialized
        if (player != null)
        {
            EngineControl.PilotEnterPlaneGlobal(player);
            //voice range change to allow talking inside cockpit (after VRC patch 1008)
            EngineControl.SeatedPlayers[ThisStationID] = player.playerId;
            if (player.isLocal)
            {
                foreach (int crew in EngineControl.SeatedPlayers)
                {//get get a fresh VRCPlayerAPI every time to prevent players who left leaving a broken one behind and causing crashes
                    VRCPlayerApi guy = VRCPlayerApi.GetPlayerById(crew);
                    if (guy != null)
                    {
                        SetVoiceInside(guy);
                    }
                }
            }
            else if (EngineControl.Piloting || EngineControl.Passenger)
            {
                SetVoiceInside(player);
            }
        }
    }
    public override void OnStationExited(VRCPlayerApi player)
    {
        if (!SeatInitialized) { InitializeSeat(); }
        PlayerExitPlane(player);
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!SeatInitialized) { InitializeSeat(); }
        if (player.playerId == EngineControl.SeatedPlayers[ThisStationID])
        {
            PlayerExitPlane(player);
        }
    }
    public void PlayerExitPlane(VRCPlayerApi player)
    {
        if (!SeatInitialized) { InitializeSeat(); }
        EngineControl.SeatedPlayers[ThisStationID] = -1;
        if (player != null)
        {
            EngineControl.PilotExitPlane(player);
            SetVoiceOutside(player);
            if (PilotOnly != null) { PilotOnly.SetActive(false); }
            if (SeatAdjuster != null) { SeatAdjuster.SetActive(false); }
            if (player.isLocal)
            {
                EngineControl.MySeat = -1;
            }
            if (player.isLocal)
            {
                //undo voice distances of all players inside the vehicle
                foreach (int crew in EngineControl.SeatedPlayers)
                {
                    VRCPlayerApi guy = VRCPlayerApi.GetPlayerById(crew);
                    if (guy != null)
                    {
                        SetVoiceOutside(guy);
                    }
                }
            }
        }
    }
    private void SetVoiceInside(VRCPlayerApi Player)
    {
        Player.SetVoiceDistanceNear(999999);
        Player.SetVoiceDistanceFar(1000000);
        Player.SetVoiceGain(.6f);
    }
    private void SetVoiceOutside(VRCPlayerApi Player)
    {
        Player.SetVoiceDistanceNear(0);
        Player.SetVoiceDistanceFar(25);
        Player.SetVoiceGain(15);
    }
    private void InitializeSeat()
    {
        EngineControl.FindSeats();
        int x = 0;
        foreach (VRCStation station in EngineControl.VehicleStations)
        {
            if (station.gameObject == gameObject)
            {
                ThisStationID = x;
                EngineControl.PilotSeat = x;
            }
            x++;
        }
        SeatInitialized = true;
    }
    private void Assert(bool condition, string message)
    {
        if (!condition)
        {
            Debug.LogWarning("Assertion failed : '" + GetType() + " : " + message + "'", this);
        }
    }
}
