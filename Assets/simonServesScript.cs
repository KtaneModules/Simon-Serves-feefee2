using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Random = UnityEngine.Random;

public class simonServesScript : MonoBehaviour
{

    public KMAudio Audio;
    public KMBombInfo Bomb;

    public KMSelectable table;
    public KMSelectable[] buttons;
    //0 -> red, 1 -> lime
    public Material[] colorsLED;
    //top to bottom 0-3
    public Renderer[] led;
    //0 -> Red, 1 -> White, 2 -> Blue, 3 -> Brown, 4 -> Green, 5 -> Yellow, 6 -> Orange, 7 -> Pink
    public Material[] colorFood;
    //6, top left to bottom left clockwise
    public Renderer[] food;
    //matching to index of buttons
    public Renderer[] marker;

    //lookup tables
    private int[,,] people = new int[6, 4, 8]
    {
        {
            {1,2,0,3,4,5,6,7},
            {0,4,2,7,6,1,5,3},
            {3,0,2,4,7,1,5,6},
            {6,3,5,4,1,0,2,7}
        },
        {
            {6,7,3,4,2,5,1,0},
            {3,1,0,5,4,6,2,7},
            {2,7,1,3,5,4,0,6},
            {1,6,7,4,0,5,2,3}
        },
        {
            {2,3,5,1,0,4,6,7},
            {2,0,1,7,3,6,5,4},
            {0,7,2,1,4,5,3,6},
            {7,6,0,4,5,1,3,2}
        },
        {
            {5,1,0,2,6,4,7,3},
            {1,3,7,6,0,2,5,4},
            {1,7,4,6,2,0,5,3},
            {0,7,4,5,3,1,6,2}
        },
        {
            {3,4,6,1,7,0,5,2},
            {4,0,6,2,7,3,5,1},
            {4,3,2,0,1,5,6,7},
            {5,1,2,4,3,6,0,7}
        },
        {
            {7,0,2,1,4,6,5,3},
            {5,4,0,7,3,2,6,1},
            {7,4,1,6,2,0,3,5},
            {3,2,0,4,1,5,6,7}
        }
    };
    private String serialNumber;

    //blinking people
    //determins wether to people should blink or not
    private bool blinkEnabled = false;
    //blinking order
    private int[] blinkingOrder = new int[7] { 0, 0, 0, 0, 0, 0, 6 };
    //index in blinkingOrder of the person who is blinking right now
    private int blinkingPreson;
    private float time;

    //game states
    //-1 -> no food served yet, 0 -> drinks, 1 -> Appetizer, 2 -> Main Course, 3 -> Dessert, 4 -> Bill, 5 -> solved
    private int stage = -1;
    //serving order in which the people should be served (stage rules already taken into consideration)
    //0 -> Red, 1 -> Blue, 2 -> Green, 3 -> Violet, 4 -> White, 5 -> Black
    private int[] servingOrder;
    //what food is on the table
    //-1 -> nothing, 0 -> Red, 1 -> White, 2 -> Blue, 3 -> Brown, 4 -> Green, 5 -> Yellow, 6 -> Orange, 7 -> Pink
    private int[] foods = new int[6];
    //backup array
    private int[] originalFoods = new int[6];
    //the index in servingOrder of the nexted guest to be served
    private int nextIndex = 0;
    //the last pressed guest
    private int lastGuestPress = -1;


    //prior game states
    private bool forgetCocktailServed = false;
    private bool mainCourseFirstPickRedWhiteGreen = false;
    private int bombBlastBoomDishes = 0;
    private int mainCourseLastPick = -1;

    //logging
    static int moduleIdCounter = 1;
    int moduleId = 0;
    private bool moduleSolved = false;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        table.OnInteract += delegate () { PressTable(); return false; };
        //init press event for guests
        for (int i = 0; i < buttons.Length; i++)
        {
            int j = i;
            buttons[i].OnInteract += delegate () { PressButton(j); return false; };
        }
        //init press event for food
        for(int i = 0; i < food.Length; i++)
        {
            int j = i;
            food[i].GetComponent<KMSelectable>().OnInteract += delegate () { PressFood(j); return false; };
        }
        foreach(Renderer r in marker)
        {
            r.enabled = false;
        }
    }

    // Use this for initialization
    void Start()
    {
        //NextStage();
        hideFood();
        serialNumber = Bomb.GetSerialNumber();
    }

    // Update is called once per frame
    void Update()
    {
        if (!blinkEnabled) return;
        time += Time.deltaTime;
        if(time > 1)
        {
            marker[blinkingOrder[blinkingPreson]].enabled = true;
        }
        if(time > 2)
        {
            marker[blinkingOrder[blinkingPreson]].enabled = false;
            time -= 2;
            blinkingPreson++;
            if(blinkingPreson >= 7) blinkingPreson = 0;
        }
        
    }

    //press on the table (not the food but the table)
    void PressTable()
    {
        if (stage == -1)
        {
            table.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
            //testing only set to 0 to skip to appetizer, 1 to skip to the main course, 2 to skip to the dessert
            //stage = 1;
            //when skipping main course uncomment
            //mainCourseLastPick = 0;
            //initiats the table
            NextStage();
            blinkEnabled = true;
        }
        else if (stage == 4)
        {
            table.AddInteractionPunch();
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            IEnumerable letters = Bomb.GetSerialNumberLetters();
            string[] names = new string[] { "riley", "brandon", "gabriel", "veronica", "wendy", "kayle" };
            int rightPerson = mainCourseLastPick;

            for(int i = 0; i < 6; i++)
            {
                foreach(char letter in letters)
                {
                    if (names[rightPerson % 6].Contains(letter.ToString().ToLower()))
                    {
                        Debug.Log("[Simon Serves] Worked");
                        goto afterLoop;
                    }
                }
                rightPerson++;
            }
            Debug.Log("[Simon Serves] Here");
            //no match found

            rightPerson %= 6;

            for (int i = 0; i < 6; i++)
            {
                if (marker[i].enabled)
                {
                    string markersEnabledLog = String.Join(" ", new List<bool>(new bool[] { marker[0].enabled, marker[1].enabled, marker[2].enabled, marker[3].enabled, marker[4].enabled, marker[5].enabled }).ConvertAll(j => j.ToString()).ToArray());
                    Debug.Log("[Simon Serves] 0" + markersEnabledLog + "; " + rightPerson + "; " + i);
                    GetComponent<KMBombModule>().HandleStrike();
                    for (int j = 0; j < 6; j++)
                    {
                        marker[j].enabled = false;
                    }
                    return;
                }
            }
            stage++;
            Audio.PlaySoundAtTransform("Module Solved", transform);
            GetComponent<KMBombModule>().HandlePass();
            return;
            afterLoop:
            Debug.Log("[Simon Serves] Here2");
            //match found
            rightPerson %= 6;
            for(int i = 0; i < 6; i++)
            {
                if((!marker[i].enabled && rightPerson == i) || (marker[i].enabled && rightPerson != i))
                {
                    string markersEnabledLog = String.Join(" ", new List<bool>(new bool[] { marker[0].enabled, marker[1].enabled, marker[2].enabled, marker[3].enabled, marker[4].enabled, marker[5].enabled }).ConvertAll(j => j.ToString()).ToArray());
                    Debug.Log("[Simon Serves] 1" + markersEnabledLog + "; " + rightPerson + "; " + i);
                    GetComponent<KMBombModule>().HandleStrike();
                    for (int j = 0; j < 6; j++)
                    {
                        marker[j].enabled = false;
                    }
                    return;
                }
            }
            stage++;
            Audio.PlaySoundAtTransform("Module Solved", transform);
            GetComponent<KMBombModule>().HandlePass();

            /*
            IEnumerable letters = Bomb.GetSerialNumberLetters();
            string[] names = new string[] { "Riley", "Brandon", "Gabriel", "Veronica", "Wendy", "Kayle" };
            int rightPerson = mainCourseLastPick;
            rightPerson += bombBlastBoomDishes;
            rightPerson %= 6;

            int rightPersonBackup = rightPerson;

            for (int i = 0; i < 6; i++)
            {
                foreach (char letter in letters)
                {
                    if (names[rightPerson % 6].Contains(letter))
                    {
                        goto afterLoop;
                    }
                }
                rightPerson++;
            }
            
            afterLoop:

            for (int i = rightPersonBackup; i <= rightPerson; i++)
            {
                if (!marker[i % 6].enabled)
                {
                    GetComponent<KMBombModule>().HandleStrike();
                    for (int j = 0; j < 6; j++)
                    {
                        marker[j].enabled = false;
                    }
                    return;
                }
            }
            */
        }
    }

    //press on the people
    void PressButton(int num)
    {
        if ((lastGuestPress != num && stage >= 0) || stage == 4)
        {
            buttons[num].AddInteractionPunch(.5f);
            if (stage < 4)
            {
                // to remove strike on wrong person selection without selecting food
                // REMOVE FROM HERE
                if (!(num == servingOrder[nextIndex]))
                {
                    Debug.Log("[Simon Serves] Wrong Person current, right on: " + num + ", " + servingOrder[nextIndex]);
                    LogGameState();
                    HandleStrike();
                    return;
                }
                // TO HERE
                Audio.PlaySoundAtTransform("Button Press " + (nextIndex * 2 + 1), transform);
            }
        }
        lastGuestPress = num;
        if (stage == 4)
        {
            marker[num].enabled = !marker[num].enabled;
        }
    }

    //press on the food in the table
    void PressFood(int foodIndex)
    {
        food[foodIndex].GetComponent<KMSelectable>().AddInteractionPunch();
        Audio.PlaySoundAtTransform("Button Press " + (nextIndex * 2 + 2), transform);
        int num = foods[foodIndex];
        if (checkFood(num))
        {
            foods[foodIndex] = -1;
            food[foodIndex].enabled = false;
            food[foodIndex].GetComponentInChildren<KMHighlightable>().transform.localScale = new Vector3((float)0.0001, (float)0.0001, (float)0.0001);
        }
        displayFood(false);
    }

    //check whether the right food was clicked
    //return whether it was the right food false does not nessecarily mean that it was a strike
    bool checkFood(int numFood)
    {
        //check for stage
        if (stage > 3 || stage < 0) return false; //we shouldn't be here...

        if (numFood == -1) return false;

        if (lastGuestPress == -1) return false;
        //correct guest selected
        if (lastGuestPress == servingOrder[nextIndex])
        {
            //check whether correct dish is selected
            for (int i = 0; i < 8; i++)
            {
                if (people[lastGuestPress, stage, i] == numFood)
                {
                    lastGuestPress = -1;
                    //correct dish
                    //no more dishes left
                    if (nextIndex < 5)
                    {
                        nextIndex++;
                        return true;
                    }
                    NextStage();
                    return (stage == 4);
                }
                if (foods.Contains(people[lastGuestPress, stage, i]))
                {
                    //incorrect dish
                    Debug.Log("[Simon Serves] Wrong Food current, right on: " + numFood + ", " + people[lastGuestPress, stage, i]);
                    LogGameState();
                    HandleStrike();
                    return false;
                }
            }
        }
        else
        {
            //incorrect guest
            Debug.Log("[Simon Serves] Wrong Person current, right on: " + lastGuestPress + ", " + servingOrder[nextIndex]);
            LogGameState();
            HandleStrike();
            return false;
        }
        return false;
    }

    //handles a strike
    void HandleStrike()
    {
        nextIndex = 0;
        lastGuestPress = -1;

        GetComponent<KMBombModule>().HandleStrike();
        RedoFood();
    }

    //rearanges the food e.g. on a strike
    void RedoFood()
    {
        Array.Copy(originalFoods, 0, foods, 0, originalFoods.Length);
        RandomizeArray(foods);
        displayFood();
    }

    //TODO: dropped for now
    //generates a random order for the people to sit in
    void generateSeats()
    {

    }

    //generates a random order for the food to be alligned on the table
    void gernerateTable()
    {
        //two unique values to not be served
        int rand1 = Random.Range(0, 8);
        int rand2 = Random.Range(0, 8);
        while (rand1 == rand2)
        {
            rand2 = Random.Range(0, 8);
        }
        //refill array with all but excluded food
        int j = 0;
        for (int i = 0; i < 8; i++)
        {
            if (i != rand1 && i != rand2)
            {
                foods[j++] = i;
            }
        }
        //clone the array for possible later use
        Array.Copy(foods, 0, originalFoods, 0, foods.Length);
        //randomize array
        RandomizeArray(foods);
    }

    //display the food currently on the table (in the foods array)
    //all slots in foods filled with -1 will be ignored
    void displayFood(bool visible = true)
    {
        for(int i = 0; i < 6; i++)
        {
            if (foods[i] == -1) continue;
            food[i].material = colorFood[foods[i]];
            if (!visible) continue;
            food[i].enabled = true;
            food[i].GetComponentInChildren<KMHighlightable>().transform.localScale = new Vector3((float)1.1, (float)1.1, (float)1);
        }
    }

    //hides all the food
    void hideFood()
    {
        for (int i = 0; i < 6; i++)
        {
            food[i].enabled = false;
            food[i].GetComponentInChildren<KMHighlightable>().transform.localScale = new Vector3((float)0.0001, (float)0.0001, (float)0.0001);
        }
    }

    //progress to the next Stage
    void NextStage()
    {
        nextIndex = 0;
        stage++;
        //turn the stage indikators on and off
        if(stage < 5 && stage >= 1)
        {
            led[stage - 1].material = colorsLED[1];
        }
        if(stage == 4)
        {
            blinkEnabled = false;
            marker[blinkingOrder[blinkingPreson]].enabled = false;
        }
        //generate the stage
        if (stage < 4 && stage >= 0)
        {
            time = 0;
            blinkingPreson = 0;
            foreach(Renderer r in marker)
            {
                r.enabled = false;
            }
            gernerateTable();
            displayFood();
            generateServingOrder();
            //TODO check whether complete
        }
    }

    //generates the order to blink the people in and the order in which to serve the people in
    //only call after generateTable was already called 
    void generateServingOrder()
    {
        int[] temp = new int[6] {0, 1, 2, 3, 4, 5};
        RandomizeArray(temp);
        Array.Copy(temp, 0, blinkingOrder, 0, temp.Length);
        
        //copys the temp array to the serving order array for manipulation
        servingOrder = new int[temp.Length];
        Array.Copy(temp, 0, servingOrder, 0, temp.Length);

        //applys the stage rules
        if (stage == 0)
        {
            int foo = 0;
            for (int i = 0; i < 8; i++)
            {
                if (temp[i % 6] == 3 || temp[i % 6] == 1 || temp[i % 6] == 0)
                {
                    foo++;
                    continue;
                }
                if(foo == 3)
                {
                    break;
                }
                foo = 0;
            }
            //not consecutive
            if (foo < 3)
            {
                swap(servingOrder, 1, 3);
            }
            //consecutive
            else
            {
                swap(servingOrder, 0, 5);
            }
            //adjust priority list if Cruelo Juice is served
            if(foods.Contains(0))
            {
                for (int i = 0; i < 6; i++)
                {
                    swap3DSimple(people, i, 0, 0, 4);
                    swap3DSimple(people, i, 0, 1, 5);
                    swap3DSimple(people, i, 0, 2, 6);
                    swap3DSimple(people, i, 0, 3, 7);
                }
            }
            //logs if Forget Cocktail was served
            if(foods.Contains(4))
            {
                forgetCocktailServed = true;
            }
            //logs dishes with bomb, blast and boom
            if (foods.Contains(3)) bombBlastBoomDishes++;
        }
        else if(stage == 1)
        {
            //counter how many consecutive guests flashes
            int foo = 0;
            //the number of the last consecutive flashing person
            int bar = -2;
            for(int i = 0; i < 8; i++)
            {
                if (foo >= 3) break;

                if(--bar == temp[i % 6])
                {
                    foo++;
                }
                else
                {
                    bar = temp[i % 6];
                    foo = 1;
                }
                bar = (bar == 0) ? 6 : bar;
            }
            //if at least 3 consecutive guests flash
            if(foo >= 3)
            {
                swap(servingOrder, 2, 3);
                swap(servingOrder, 1, 4);
            }
            else
            {
                swap(servingOrder, 0, 5);
                swap(servingOrder, 3, 5);
                swap(servingOrder, 2, 4);
            }
            //adjust priority list based on whether Boolean Waffles are served
            bool cont = foods.Contains(7) && foods.Contains(6);
            for (int i = 0; i < 6; i++)
            {
                if ((cont && (i == 1 || i == 3 || i == 4)) || (!cont && (i == 0 || i == 2 || i == 5)))
                {
                    swap3DSimple(people, i, 1, 0, 7);
                    swap3DSimple(people, i, 1, 1, 6);
                    swap3DSimple(people, i, 1, 2, 5);
                    swap3DSimple(people, i, 1, 3, 4);
                }
            }
            //logs dishes with bomb, blast and boom
            if (foods.Contains(3)) bombBlastBoomDishes++;
            if (foods.Contains(5)) bombBlastBoomDishes++;
        }
        else if(stage == 2)
        {
            //opposite seated counter
            int foo = 0;
            //last guests number
            int bar = -4;
            //
            int[,] pairs = new int[3,2] { {-1, -1}, {-1, -1}, {-1, -1} };
            
            for(int i = 0; i < 7; i++)
            {
                if (5 - bar == temp[i % 6])
                {
                    pairs[foo, 0] = i - 1;
                    pairs[foo, 1] = i % 6;
                    foo++;
                }
                else
                {
                    bar = temp[i % 6];
                }
            }
            //if at least 2 opposite seated pairs of people. Swap them
            if (foo >= 2)
            {
                for (foo--; foo >= 0; foo--)
                {
                    swap(servingOrder, pairs[foo, 0], pairs[foo, 1]);
                }
            }
            else
            {
                swap(servingOrder, 0, 5);
                swap(servingOrder, 1, 4);
                swap(servingOrder, 2, 3);
            }
            //adjust priority list based on whether Forghetti Bombognese are served
            if(foods.Contains(0) && forgetCocktailServed)
            {
                for (int i = 0; i < 6; i++)
                {
                    swap3DSimple(people, i, 2, 0, 2);
                    swap3DSimple(people, i, 2, 1, 5);
                    swap3DSimple(people, i, 2, 4, 6);
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    swap3DSimple(people, i, 2, 0, 3);
                    swap3DSimple(people, i, 2, 1, 2);
                    swap3DSimple(people, i, 2, 4, 7);
                    swap3DSimple(people, i, 2, 5, 6);
                }
            }
            //logs if the first served guest was red, white or green
            if (servingOrder[0] == 0 || servingOrder[0] == 4 || servingOrder[0] == 2)
            {
                mainCourseFirstPickRedWhiteGreen = true;
            }
            //logs dishes with bomb, blast and boom
            if (foods.Contains(0)) bombBlastBoomDishes++;
            if (foods.Contains(6)) bombBlastBoomDishes++;
            if (foods.Contains(7)) bombBlastBoomDishes++;
            //logs last pick
            mainCourseLastPick = servingOrder[5];
        }
        else
        {
            //addapting serving order according to the rules
            if (mainCourseFirstPickRedWhiteGreen)
            {
                swap(servingOrder, 0, 3);
                swap(servingOrder, 1, 5);
                swap(servingOrder, 2, 4);
            }
            else
            {
                swap(servingOrder, 0, 2);
                swap(servingOrder, 3, 5);
            }
            // adjust priority list based on whether Bamboozling Waffles are served
            if (foods.Contains(6) && foods.Contains(0))
            {
                for (int i = 0; i < 6; i++) {
                    swap3DSimple(people, i, 3, 1, 2);
                    swap3DSimple(people, i, 3, 1, 7);
                    swap3DSimple(people, i, 3, 4, 7);
                    swap3DSimple(people, i, 3, 3, 6);
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    swap3DSimple(people, i, 3, 0, 1);
                    swap3DSimple(people, i, 3, 2, 3);
                    swap3DSimple(people, i, 3, 4, 5);
                    swap3DSimple(people, i, 3, 6, 7);
                }
            }
            //logs dishes with bomb, blast and boom
            if (foods.Contains(1)) bombBlastBoomDishes++;
            if (foods.Contains(7)) bombBlastBoomDishes++;
        }
    }

    void RandomizeArray<T>(T[] array)
    {
        int n = array.Length;
        while (n > 1)
        {
            int k = Random.Range(0, n--);
            T temp = array[n];
            array[n] = array[k];
            array[k] = temp;
        }
    }

    void swap<T>(T[] array, int pos1, int pos2)
    {
        if (pos1 >= array.Length || pos2 >= array.Length) return;
        T temp = array[pos1];
        array[pos1] = array[pos2];
        array[pos2] = temp;
    }

    void swap3DSimple<T>(T[,,] array, int index1, int index2, int pos1, int pos2)
    {
        T temp = array[index1, index2, pos1];
        array[index1, index2, pos1] = array[index1, index2, pos2];
        array[index1, index2, pos2] = temp;
    }

    //blinkingOrderLog + "; " + servingOrderLog + "; " + stage + "; " + foodsLog + "; " + originalFoodsLog + "; " + nextIndex + "; " + prioListLog
    void LogGameState()
    {
        string blinkingOrderLog = String.Join(" ", new List<int>(blinkingOrder).ConvertAll(i => i.ToString()).ToArray());
        string servingOrderLog = String.Join(" ", new List<int>(servingOrder).ConvertAll(i => i.ToString()).ToArray());
        string foodsLog = String.Join(" ", new List<int>(foods).ConvertAll(i => i.ToString()).ToArray());
        string originalFoodsLog = String.Join(" ", new List<int>(originalFoods).ConvertAll(i => i.ToString()).ToArray());
        string prioListLog = "nope";
        if (lastGuestPress != -1 && stage >= 0 && stage < 4) prioListLog = String.Join(" ", new List<int>(new int[] { people[lastGuestPress, stage, 0], people[lastGuestPress, stage, 1], people[lastGuestPress, stage, 2], people[lastGuestPress, stage, 3], people[lastGuestPress, stage, 4], people[lastGuestPress, stage, 5], people[lastGuestPress, stage, 6], people[lastGuestPress, stage, 7] }).ConvertAll(i => i.ToString()).ToArray());

        Debug.Log("[Simon Serves] " + blinkingOrderLog + "; " + servingOrderLog + "; " + stage + "; " + foodsLog + "; " + originalFoodsLog + "; " + nextIndex + "; " + prioListLog);
    }
}
