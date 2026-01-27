//This program reads in the 'browser map' data that warthunder emits to provide a voice warnings.
//You can swith out the voice warnings with your own just by swapping out the wav files in the same folder as the executable.


//AI disclaimer - some minor edits in this file were made with AI.


using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Aircraft_Voice_Warning_System_for_Warthunder_on_Linux;
//Note that under 8111/indicators I can get type info if I want to set custom overrides.
class Program
{
    static private TimeSpan TargetDelta = new TimeSpan(0,0,0,0,1000/10,0);
    static private DateTime ThresholdTime = DateTime.UtcNow;

    static private HttpClient DataInHttpClient = new HttpClient();
    static private Uri StateDataInEndpoint = new Uri("http://localhost:8111/state");

    static private Task<HttpResponseMessage>? DataInTask;

    static private HttpResponseMessage? DataInTaskResult;

    static private Stream? DataInContentStream;

    static private StreamReader? TextDataInStream;

    static private string? TextInComplete;

    static private string [] StateSplitters = new string [] {"{\"","}",",\r\n\"",",\n\""};
    static private string [] ItemSplitters = new string [] {"\": "};

    
    static private AircraftState? CurrentAircraftState;

    static private AudioPlayer TheAudioPlayer = new AudioPlayer();

    static void Main(string[] args)
    {        
    Console.ForegroundColor = ConsoleColor.White;
    Console.BackgroundColor = ConsoleColor.Black;
    Console.WriteLine(@"THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.");
    Console.WriteLine("");
    Console.ForegroundColor = ConsoleColor.White;
    Console.BackgroundColor = ConsoleColor.DarkBlue;
    Console.WriteLine("Aircraft Voice Warning System for Warthunder on Linux.");
    Console.WriteLine("Programmed in Australia, with booring parts delegated to AI, and checked by a real live human.");
    Console.WriteLine("");    


    bool PrintDebug = false;
    if(args.Length > 0){
        SortedSet<string> ArgsSorted = new SortedSet<string>();
        foreach(string arg in args)
        {
            ArgsSorted.Add(arg.ToUpperInvariant());
        }
            if (ArgsSorted.Contains("DEBUG"))
            {
                PrintDebug = true;
            }
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.BackgroundColor = ConsoleColor.Black;

    Console.WriteLine("Precaching Sounds:");
     
    PrecacheSounds();

    Console.WriteLine("System active.");

    PrettyPrintTriggerThresholds();

    //Happy to have a proper use for a goto; GOTO's are fun :)
    CoreLoopStart:

        try{

        //Wait to not spam
        Spinwait();

        //Get Input
        if (!RecieveData())
        {
            goto CoreLoopStart;    
        }

        ////Debug print
        //CurrentAircraftState.PrintState();

        //Action
        ProcessData();

        }
        catch(Exception ex)
        {
            if(PrintDebug){
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Encountered an error");
                Console.WriteLine(ex.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
            }

            goto CoreLoopStart;    
        }
        goto CoreLoopStart;


    }

    private static void Spinwait()
    {
        while(DateTime.Now < ThresholdTime)//Spin wait
        {
            System.Threading.Thread.Sleep(0);            
        }

        ThresholdTime = ThresholdTime + TargetDelta;//Try to select next jump with no extra latency from coming out of the spin wait

        if(ThresholdTime >= DateTime.Now)//But if we are seriously laggin behind, then accept the latencey now for a chance to be able to recover in the future
        {
            ThresholdTime = DateTime.Now + TargetDelta;
        }
    }



    //This gets the aircraft's state
    //There's other endpoints that I could also pull additional data from in a future iteration.
    private static bool RecieveData ()
    {
        //Get STATE

        DataInTask = DataInHttpClient.GetAsync(StateDataInEndpoint);

        DataInTask.Wait();

        if (!DataInTask.IsCompletedSuccessfully)
        {
            return false;
        }

        DataInTaskResult = DataInTask.Result;

        DataInContentStream = DataInTaskResult.Content.ReadAsStream();

        TextDataInStream = new StreamReader(DataInContentStream);

        TextInComplete = TextDataInStream.ReadToEnd();

        string [] StateChunks = TextInComplete.Split(StateSplitters, StringSplitOptions.RemoveEmptyEntries);

        CurrentAircraftState = new AircraftState();

        foreach(string Chunk in StateChunks)
        {
            string [] Elements = Chunk.Split(ItemSplitters, StringSplitOptions.RemoveEmptyEntries);

            if(Elements.Length != 2)
            {
                continue;
            }
            float Result = 0.0f;
            if(!Single.TryParse(Elements[1], out Result))
            {
                continue;
            }

            //AI Disclaimer - Switch filled out with AI because who want's to do something that booring?
            switch (Elements[0]){
                case "aileron, %":
                    CurrentAircraftState.Aileron = Result;
                    break;
                case "elevator, %":
                    CurrentAircraftState.Elevator = Result;
                    break;
                case "rudder, %":
                    CurrentAircraftState.Rudder = Result;
                    break;
                case "flaps, %":
                    CurrentAircraftState.Flaps = Result;
                    break;
                case "gear, %":
                    CurrentAircraftState.Gear = Result;
                    break;
                case "H, m":
                    CurrentAircraftState.Alititude_Above_Sea_Level = Result;
                    break;
                case "TAS, km/h":
                    CurrentAircraftState.TAS_kmh = Result;
                    break;
                case "IAS, km/h":
                    CurrentAircraftState.IAS_kmh = Result;
                    break;
                case "M":
                    CurrentAircraftState.Mach = Result;
                    break;
                case "AoA, deg":
                    CurrentAircraftState.Angle_Of_Attack = Result;
                    break;
                case "AoS, deg":
                    CurrentAircraftState.Angle_Of_Slip = Result;
                    break;
                case "Ny":
                    CurrentAircraftState.G_Force_On_Lift_Vector_mss = Result;
                    break;
                case "Vy, m/s":
                    CurrentAircraftState.Climb_Rate_ms = Result;
                    break;
                case "Wx, deg/s":
                    CurrentAircraftState.Roll_Rate_dps = Result;
                    break;
                case "Mfuel, kg":
                    CurrentAircraftState.Current_Fuel_Weight_kg = Result;
                    break;
                case "Mfuel0, kg":
                    CurrentAircraftState.Maximum_Fuel_Weight_kg = Result;
                    break;
            }
        }



        return true;
    }



    //We only want one warning to play at a time
    //And the highest priority warning should play immediately, overriding any warning that is of lower priority
    private static int CurrentWarningLevel = -1;    


    //For our fuel warnings, we use a latch so that they only call once when we hit the critical threshold
    //We reset when fuel amount increases (i.e. respawn, as WT doesn't have AAR)
    private static bool JokerTriggered = false;
    private static float JokerLevel = -1.0f;
    private static bool BingoTriggered = false;
    private static float BingoLevel = -1.0f;
    private static float CurrentFuelLastFuel = -1.0f;


    //We guesstimate the airfield altitude for a best guess at ground level as we can't get height AGL from Warthunder's endpoints.
    //This is a good thing as it protects the community against cheaters who could exploit that data to auto-bomb.
    //However becase we don't have radalt, the warnings could be a bit sketchy if (1) you airstart and the field is not near the previous estimated altitude or (2) you take of from a low altitude field and then operate in a high altitude area.
    private static float AirfieldAltitude = -1.0f;

    private static float LastFuelMax = -1.0f;



    //Trigger thresholds

    //Over G warning - I imagine you'd want to be quite high for 13g aircraft but quite low for more fragile birds (black widow, flying dorito, Ki-43).
    //I figure 5G is safe for most planes (looking at you with concern SR-71) so it can serve as an early onset warning, but depending on what you're looking for it could be unwanted.
    private static float OverGPositive = 5.0f; 
        //G force
    private static float OverGNegative = -5.0f; 
        //G force


    private static float OverspeedThreshold = 700.0f;
        //km/hr
        //Probably the main thing you'd want to modify from plane to plane, after G-limits.
        //700 is probably ok for a lot of props, as you don't get up to this outside of a significant dive.
        //But if you're in a jet then this is cruizing speed (only 'bout 350kts).

    private static float Angle_Of_Attack_Threshold_Positive = 15.0f;
        //Degrees of Angle of Attack
    private static float Angle_Of_Attack_Threshold_Negative = 15.0f;
        //Degrees of Angle of Attack

        //Angle of attack threshold 
        //15 should be good for most wings. From DCS hornet driving I feel stall onsets more seriously around 20, and if you get up into the 40s, something funky is going on.
        //This probably will only be a bit much if you have a really strange wing. Maybe spitfires and yaks could have an issue with their semi-eliptical wings, I don't typically fly them so unsure.

    private static float Ignore_Angle_Of_Attack_Warning_Below_Speed = 30.0f;
        //kph    
        //Don't scream AoA warnings when chilling on the runway. Also not much point calling out AoA when doing a zero-speed flop at the top of a boom climb (you should darn well know what's happening at that point anyway - no need for extra distractions).
    
    private static float Angle_Of_Slip_Threshold = 10.0f;
        //Degrees of slip (symmeterical)
        //To get into 10 degrees of slip you're either doing a very uncoordinated turn, got battle damage, or given it a full boot of rudder.
        //If you wanted feedback to train yourself to fly more coordinated, you could turn this down to say 4 degrees, and get a warning when you've forgotten your rudder input.


    private static float Any_Flaps_Limit = 400.0f;
        //kph
        //If flaps are at all down warn above this speed

    private static float Flaps_User_Threshold = 20.0f;
        //Percent, 0 up 100 all the way down
        //A threshold for being worried about seriously breaking your flaps
        //This is the one to warn you forgot your takeoff/landing flaps are down.

    private static float User_Threshold_Flaps_Limit = 300.0f;
        //kph
        //Limit associated with the user theshold
        //Note that for the corsair this needs to be something crazy low. Maybe 220kph? Most planes in WT can handle 300 though, and the use cases for full flap above this speed are uncommon.

    private static float Gear_Overspeed_Threshold = 270.0f;
        //kph threshold for gear overspeed warning
        //Seems to be fine
        //Increase to Mach 2 if you're flying a corsair ;)

    private static float Check_Gear_Threshold = 270.0f;
        //kph threshold
        //Did you forget to put your gear down for landing
        //Only kicks in when decending, gear up, below the altitude threshold, and below this airspeed threshold

    private static float Check_Gear_Altitude_Threshold = 150.0f;
        //meters of altitude
        //If you're decending, gear up below this altitude and below the check gear threshold speed, get a check gear warning
        //Unfortunately this could be triggered when below your starting airfield but well above ground (e.g. start on a mountain).
        //However, typical low flight is faster than the check gear threshold so your probably going to be ok.

    


    //Terrain warning
        //I expect the pilot to be switched on enough that no warning is necessary for easily recoverable gentle decents
        //Remember, this not an Auto-TCAS, it's just calibarated baro altitude warning that but could save your neck diving through low cloud, or snap you out of target fixation.

    private static float SecondsToImpactForPullUpWarning = 5.0f;
        //Seconds to hit the calibrated baro floor at your current climb rate
        //More manuverable planes could handle a lower setting, less manuverable planes or those which lock up in a dive might need a little more
        //For a slow but maneuverable CAS bird 5 seconds is probably a little to much, depends if you want to use the warning for 'act now', or as a timing thing.
    private static float MinClimbRateForPullUpWarning = -5.0f;
        //m/s climb rate (typically you'd want this to be -ive to show a decent) this indicates we are in a rapid decent, and not a landing profile.
    
    private static float PullupPullupVsPowerPower_SpeedThreshold = 250.0f;
        //km/hr
        //When slow and projected to hit the ground, scream POWER! POWER! to kick on throttle so that the bird can perform a min radius turn rather than stall out of the sky.
        //As I don't have radalt this is not as well calibrated as you'd get in something like a DCS F/A-18C.

    
    //Fuel limits config

    private static float JokerFuelProportion = 0.333f;
        //proportion of full fuel weight
        //Should be ~12 min for a 35min tank
        //First warning, joker fuel is your 'time to wrap up this mission quickly' warning.
        
    private static float BingoFuelProportion = 0.166f;
        //proportion of full fuel weight
        //Should be ~6min for a 35min tank
        //Second warning, bingo fuel is 'need to go home now or divert' warning.

    

    


    //AI Disclaimer - function created by AI and edited by me
    private static void PrettyPrintTriggerThresholds()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine("Current configuration:");
        //Console.WriteLine("Configuration floats:");
        //Console.WriteLine($"  JokerLevel: {JokerLevel:F3}");
        //Console.WriteLine($"  BingoLevel: {BingoLevel:F3}");
        //Console.WriteLine($"  CurrentFuelLastFuel: {CurrentFuelLastFuel:F3}");
        //Console.WriteLine($"  AirfieldAltitude: {AirfieldAltitude:F3}");
        //Console.WriteLine($"  LastFuelMax: {LastFuelMax:F3}");
        Console.WriteLine($"  OverGPositive: {OverGPositive:F2}");
        Console.WriteLine($"  OverGNegative: {OverGNegative:F2}");
        Console.WriteLine($"  OverspeedThreshold: {OverspeedThreshold:F1}");
        Console.WriteLine($"  Angle_Of_Attack_Threshold_Positive: {Angle_Of_Attack_Threshold_Positive:F1}");
        Console.WriteLine($"  Angle_Of_Attack_Threshold_Negative: {Angle_Of_Attack_Threshold_Negative:F1}");
        Console.WriteLine($"  Ignore_Angle_Of_Attack_Warning_Below_Speed: {Ignore_Angle_Of_Attack_Warning_Below_Speed:F1}");
        Console.WriteLine($"  Angle_Of_Slip_Threshold: {Angle_Of_Slip_Threshold:F1}");
        Console.WriteLine($"  Any_Flaps_Limit: {Any_Flaps_Limit:F1}");
        Console.WriteLine($"  Flaps_User_Threshold: {Flaps_User_Threshold:F1}");
        Console.WriteLine($"  User_Threshold_Flaps_Limit: {User_Threshold_Flaps_Limit:F1}");
        Console.WriteLine($"  Gear_Overspeed_Threshold: {Gear_Overspeed_Threshold:F1}");
        Console.WriteLine($"  Check_Gear_Threshold: {Check_Gear_Threshold:F1}");
        Console.WriteLine($"  Check_Gear_Altitude_Threshold: {Check_Gear_Altitude_Threshold:F1}");
        Console.WriteLine($"  SecondsToImpactForPullUpWarning: {SecondsToImpactForPullUpWarning:F1}");
        Console.WriteLine($"  MinClimbRateForPullUpWarning: {MinClimbRateForPullUpWarning:F1}");
        Console.WriteLine($"  PullupPullupVsPowerPower_SpeedThreshold: {PullupPullupVsPowerPower_SpeedThreshold:F1}");
        Console.WriteLine($"  JokerFuelProportion: {JokerFuelProportion:F3}");
        Console.WriteLine($"  BingoFuelProportion: {BingoFuelProportion:F3}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
    }

    private static void ProcessData ()
    {
        if (!TheAudioPlayer.IsPlaying())
        {
            CurrentWarningLevel = -1;
        }

        if(CurrentAircraftState is null){ throw new Exception("I give up. The universe no longer makes sense. Must have been hit by a cosmic ray or have a hardware fault."); }

        //Set Joker fuel to 1/3rd and Bingo fuel to 1/6th whenever we fuel up
        //This corresponds to just under 12 minutes for Joker and just under 6 minutes for Bingo
        if(CurrentAircraftState.Current_Fuel_Weight_kg > CurrentFuelLastFuel + 0.1 || LastFuelMax != CurrentAircraftState.Maximum_Fuel_Weight_kg)//Either the amount of fuel has increased (respawn) or maximum fuel has changed (respawn in a different aircraft)
        {
            JokerLevel = CurrentAircraftState.Current_Fuel_Weight_kg * JokerFuelProportion;
            BingoLevel = CurrentAircraftState.Current_Fuel_Weight_kg * BingoFuelProportion;
            BingoTriggered = false;
            JokerTriggered = false;
        }

        //Set the airfield altitude when we are not moving fast (suggests on the ground), at low G (suggests on the ground), not rapidly rolling (suggests on the ground), not burning fuel (suggests engine off/low), and not rapidly climbing / decending, and the gear is down.
        //Technically this could get messed up by doing a vertical fly up into stall with the engine off and the gear down.
        if(1==1
            && CurrentAircraftState.G_Force_On_Lift_Vector_mss > 0.9f
            && CurrentAircraftState.G_Force_On_Lift_Vector_mss < 1.2f
            && CurrentAircraftState.Gear > 99.0f
            && CurrentAircraftState.IAS_kmh < 100.0f
            && CurrentAircraftState.IAS_kmh > -100.0f
            && CurrentAircraftState.Climb_Rate_ms < 0.1f
            && CurrentAircraftState.Climb_Rate_ms > -0.1f
            && CurrentAircraftState.Current_Fuel_Weight_kg == CurrentFuelLastFuel //Fortunately doesn't leak for healthy planes in WT
            && CurrentAircraftState.Roll_Rate_dps < 10.0f
            && CurrentAircraftState.Roll_Rate_dps > -10.0f
            )
        {
            AirfieldAltitude = CurrentAircraftState.Alititude_Above_Sea_Level;
        }




        //Would benefit from per-plane details
        //OverG - warning level 11 - kick in at +-5 G
        //If you're doing this you're likely pulling up so pull up not a problem.
        if(CurrentWarningLevel < 11)
        {
            if(CurrentAircraftState.G_Force_On_Lift_Vector_mss > OverGPositive || CurrentAircraftState.G_Force_On_Lift_Vector_mss < OverGNegative)
            {
                TheAudioPlayer.Play("./Over_G.wav");    
                CurrentWarningLevel = 11;
            }
        }



        //Pull up warning level 10
        //If you pull up you slow down so overspeed not a problem
        if(CurrentWarningLevel < 10)
        {
            if((CurrentAircraftState.Alititude_Above_Sea_Level - AirfieldAltitude) <  SecondsToImpactForPullUpWarning /*Grace seconds*/ * -1.0f /*Convert climb to decent*/ * CurrentAircraftState.Climb_Rate_ms)
            {
                if(CurrentAircraftState.Climb_Rate_ms > MinClimbRateForPullUpWarning)
                {
                    //Landing profile - no warning            
                }
                else if(CurrentAircraftState.IAS_kmh > PullupPullupVsPowerPower_SpeedThreshold){
                    TheAudioPlayer.Play("./Pull_Up.wav");    
                    CurrentWarningLevel = 10;
                }
                else
                {
                    TheAudioPlayer.Play("./Power_Power.wav");    
                    CurrentWarningLevel = 10;
                }
            }
        }




        //Would benefit from per-plane details
        //Overspeed - warning level 9 - kick in at 700kph
        //If you're in overspeed you probably don't have AoA issues
        if(CurrentWarningLevel < 9)
        {
            if(CurrentAircraftState.IAS_kmh > OverspeedThreshold)
            {
                TheAudioPlayer.Play("./Over_Speed.wav");    
                CurrentWarningLevel = 9;
            }
        }


        //AoA - warning level 8 - kick in at +-15 AoA
        //Stalling worse than misconfigured landing
        if(CurrentWarningLevel < 8)
        {
            if(CurrentAircraftState.Angle_Of_Attack > Angle_Of_Attack_Threshold_Positive || CurrentAircraftState.Angle_Of_Attack < Angle_Of_Attack_Threshold_Negative)
            {
                if(CurrentAircraftState.IAS_kmh > Ignore_Angle_Of_Attack_Warning_Below_Speed || CurrentAircraftState.IAS_kmh < -Ignore_Angle_Of_Attack_Warning_Below_Speed){
                    TheAudioPlayer.Play("./Angle_Of_Attack.wav");    
                    CurrentWarningLevel = 8;
                }
            }
        }

        //AoS - warning level 7 - kick in at +-10 AoS
        //Tip-over-slip worse than minconfigured landing, but more benign than stall
        if(CurrentWarningLevel < 7)
        {
            if(CurrentAircraftState.Angle_Of_Slip > Angle_Of_Slip_Threshold || CurrentAircraftState.Angle_Of_Slip < -Angle_Of_Slip_Threshold)
            {
                if(CurrentAircraftState.IAS_kmh > Ignore_Angle_Of_Attack_Warning_Below_Speed || CurrentAircraftState.IAS_kmh < -Ignore_Angle_Of_Attack_Warning_Below_Speed){
                    TheAudioPlayer.Play("./Slip_Angle.wav");    
                    CurrentWarningLevel = 7;
                }
            }
        }

        //Flap overspeed - warning level 5
        //Flap rip can put you leathally on your side at low alt, so worse than gear rip
        if(CurrentWarningLevel < 5)
        {
            if((CurrentAircraftState.Flaps > 0.1f && CurrentAircraftState.IAS_kmh > Any_Flaps_Limit) || (CurrentAircraftState.Flaps > Flaps_User_Threshold && CurrentAircraftState.IAS_kmh > User_Threshold_Flaps_Limit))   
            {           
                    TheAudioPlayer.Play("./Flap_Limit.wav");    
                    CurrentWarningLevel = 5;
            }        
        }


        //Gear overspeed - warning level 4 - kick in at 270kph with gear not fully up
        //Gear rip is not good, and the reason why I made this in the first place. I get distracted too easily.
        if(CurrentWarningLevel < 4)
        {
            if(CurrentAircraftState.Gear > 0.1 && CurrentAircraftState.IAS_kmh > Gear_Overspeed_Threshold)
            {
                TheAudioPlayer.Play("./Gear_Limit.wav");    
                CurrentWarningLevel = 4;
            }
        }


        //Check gear - warning level 3 - landing profile and no gear down
        //Just a friendly reminder. You may want to land gear-up in some situations.
        if(CurrentWarningLevel < 3)
        {
            if(CurrentAircraftState.Gear < 0.1 && CurrentAircraftState.IAS_kmh < Check_Gear_Threshold && CurrentAircraftState.Climb_Rate_ms < 0 && CurrentAircraftState.Alititude_Above_Sea_Level - AirfieldAltitude < Check_Gear_Altitude_Threshold)
            {
                TheAudioPlayer.Play("./Check_Gear.wav");    
                CurrentWarningLevel = 3;
            }
        }


        //Bingo - warning level 2
        //Not an immediate risk. Just a heya you're going to run out of fuel. So this is less priority than all the immediate danger warnings.
        if(CurrentWarningLevel < 2)
        {
            if((CurrentAircraftState.Current_Fuel_Weight_kg < BingoLevel) && (BingoTriggered == false))
            {
                TheAudioPlayer.Play("./Bingo_Fuel.wav");    
                CurrentWarningLevel = 2;
                BingoTriggered = true;
                JokerTriggered = true;
            }
        }

        //Joker - warning level 1
        //Like bingo but even lower risk.
        if(CurrentWarningLevel < 1)
        {
            if((CurrentAircraftState.Current_Fuel_Weight_kg < JokerLevel) && (JokerTriggered == false))
            {
                TheAudioPlayer.Play("./Joker_Fuel.wav");    
                CurrentWarningLevel = 1;
                JokerTriggered = true;
            }
        }

        //Keep track of what the last fuel was for resetting.
        //Technically if you respawn in a plane with less fuel things could get mixed up.
        CurrentFuelLastFuel = CurrentAircraftState.Current_Fuel_Weight_kg;    
        LastFuelMax = CurrentAircraftState.Maximum_Fuel_Weight_kg;


        

    }


    private static void PrecacheSounds()
    {
//        TheAudioPlayer.Precache("./TestSound.wav");
        //AI disclaimer, list made with AI because me be lazy.
        TheAudioPlayer.Precache("./Over_G.wav");
        TheAudioPlayer.Precache("./Pull_Up.wav");
        TheAudioPlayer.Precache("./Power_Power.wav");
        TheAudioPlayer.Precache("./Over_Speed.wav");
        TheAudioPlayer.Precache("./Angle_Of_Attack.wav");
        TheAudioPlayer.Precache("./Slip_Angle.wav");
        TheAudioPlayer.Precache("./Flap_Limit.wav");
        TheAudioPlayer.Precache("./Gear_Limit.wav");
        TheAudioPlayer.Precache("./Check_Gear.wav");
        TheAudioPlayer.Precache("./Bingo_Fuel.wav");
        TheAudioPlayer.Precache("./Joker_Fuel.wav");
    }



}




public class AircraftState
{
    public float Aileron;
    public float Elevator;
    public float Rudder;
    public float Flaps;
    public float Gear;
    public float Alititude_Above_Sea_Level;
    public float TAS_kmh;
    public float IAS_kmh;
    public float Mach;
    public float Angle_Of_Attack;
    public float Angle_Of_Slip;

    public float G_Force_On_Lift_Vector_mss;

    public float Climb_Rate_ms;

    public float Roll_Rate_dps;

    public float Current_Fuel_Weight_kg;

    public float Maximum_Fuel_Weight_kg;

    //Could add engine stuff in the future, but I would need to work out all the specs


    //AI Disclaimer - function made with AI because I'm too lazy to type out this list.
    /// <summary>
    /// Prints all member variables of the AircraftState to the console in a formatted manner.
    /// </summary>
    public void PrintState()
    {
        Console.WriteLine("=== Aircraft State ===");
        Console.WriteLine($"Aileron:                      {Aileron:F2}%");
        Console.WriteLine($"Elevator:                     {Elevator:F2}%");
        Console.WriteLine($"Rudder:                       {Rudder:F2}%");
        Console.WriteLine($"Flaps:                        {Flaps:F2}%");
        Console.WriteLine($"Gear:                         {Gear:F2}%");
        Console.WriteLine($"Altitude Above Sea Level:     {Alititude_Above_Sea_Level:F2} m");
        Console.WriteLine($"True Air Speed:               {TAS_kmh:F2} km/h");
        Console.WriteLine($"Indicated Air Speed:          {IAS_kmh:F2} km/h");
        Console.WriteLine($"Mach:                         {Mach:F3}");
        Console.WriteLine($"Angle of Attack:              {Angle_Of_Attack:F2}°");
        Console.WriteLine($"Angle of Slip:                {Angle_Of_Slip:F2}°");
        Console.WriteLine($"G Force (Lift Vector):        {G_Force_On_Lift_Vector_mss:F2} m/s²");
        Console.WriteLine($"Climb Rate:                   {Climb_Rate_ms:F2} m/s");
        Console.WriteLine($"Roll Rate:                    {Roll_Rate_dps:F2}°/s");
        Console.WriteLine($"Current Fuel Weight:          {Current_Fuel_Weight_kg:F2} kg");
        Console.WriteLine($"Maximum Fuel Weight:          {Maximum_Fuel_Weight_kg:F2} kg");
        Console.WriteLine("======================");
    }



    
}
