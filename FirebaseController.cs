using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using TMPro;
using Firebase.Extensions;
using Singleton;
/// <summary>
/// This script is responsible for firebase events,which follows a singleton pattern
/// </summary>

public class FirebaseController: Singleton<FirebaseController>
{

    #region References

    FirebaseAuth firebaseAuth;
    FirebaseFirestore db;

    public bool SFXSoundsOn = true;
    public TextMeshProUGUI errorText;
    public int value;

    string userId;
    bool userNameExists;
    float textUpdateTimer;
    bool newTextUpdate;

    List<int> lvlList = new List<int>();
    string uEmail, uId;
    #endregion

    #region States Enum
    public enum SignUpStates
    {
        None,
        Password_Not_Strong,
        Wrong_Email_Format,
        Error,
        Success,
        No_Name,
        Name_Mismatched,
        Other

    }
    public enum SignInStates
    {
        None,
        Error,
        Success,
        Other

    }
    public enum SetNewPassword
    {
        None,
        Success,
        Other,
    }
    #endregion

    #region States References
    public SignUpStates localSignUp = SignUpStates.None;
    public SignInStates localSignIn = SignInStates.None;
    public SetNewPassword localSetNewPassword = SetNewPassword.None;
    #endregion

    #region Monobehaviour Callbacks
    private void Awake(){
      
        firebaseAuth = FirebaseAuth.DefaultInstance;
        db = FirebaseFirestore.DefaultInstance;
       
    }

    private void Update()
    {

        if (errorText.text.Length > 1 && !newTextUpdate)
        {
            newTextUpdate = true;
            textUpdateTimer = 5f;
        }

        if (textUpdateTimer > 0)
        {
            textUpdateTimer -= Time.deltaTime;
            errorText.transform.parent.gameObject.SetActive(true);
        }
        else
        {
            errorText.text = "";
            newTextUpdate = false;
            errorText.transform.parent.gameObject.SetActive(false);

        }
        if (localSignUp != SignUpStates.None)
        {
            if (localSignUp == SignUpStates.Wrong_Email_Format)
            {
                errorText.text = "Incorrect email format...";

            }
            else if (localSignUp == SignUpStates.Password_Not_Strong)
            {
                errorText.text = "Password must be at least 6 characters";

            }
            else if (localSignUp == SignUpStates.Success)
            {

                errorText.text = "Success...";

                //PlayerScript.playerScript.signUpEmailId.text = "";
                //PlayerScript.playerScript.signUpUserName.text = "";
                //PlayerScript.playerScript.signUpUserPassword.text = "";
                //PlayerScript.playerScript.signInEmailId.text = "";
                //PlayerScript.playerScript.signInUserPassword.text = "";


                LoggedIn(PlayerScript.Instance.signUpUserName.text);

            }
            else if (localSignUp == SignUpStates.Other)
            {
                errorText.text = "Error while user, or username already exists...";
            }
            else if (localSignUp == SignUpStates.Error)
            {
                errorText.text = "Error while creating or email already exists...";
            }
            else if (localSignUp == SignUpStates.No_Name)
            {
                errorText.text = "user name cannot be empty...";
            }
            else if (localSignUp == SignUpStates.Name_Mismatched)
            {
                errorText.text = "user name already exists...";
            }

            localSignUp = SignUpStates.None;




        }
        if (localSignIn != SignInStates.None)
        {
            if (localSignIn == SignInStates.Other)
            {
                errorText.text = "Credentials mismatched, try again...";

            }
            if (localSignIn == SignInStates.Success)
            {
                PlayerPrefs.SetString("userId", userId);
                errorText.text = "Success...";
                Data signUpData = new Data();
                signUpData = JsonUtility.FromJson<Data>(GameData.metaData);


                //PlayerScript.playerScript.signUpEmailId.text = "";
                //PlayerScript.playerScript.signUpUserName.text = "";
                //PlayerScript.playerScript.signUpUserPassword.text = "";
                //PlayerScript.playerScript.signInEmailId.text = "";
                //PlayerScript.playerScript.signInUserPassword.text = "";
                LoggedIn(signUpData.userName);

            }
            localSignIn = SignInStates.None;
        }

        if (localSetNewPassword != SetNewPassword.None)
        {
            if (localSetNewPassword == SetNewPassword.Other)
            {
                errorText.text = "Error while sending,check your mail id...";

            }
            if (localSetNewPassword == SetNewPassword.Success)
            {
                errorText.text = "Password reset link sent to the email...";

            }
            localSetNewPassword = SetNewPassword.None;
        }
    }
    #endregion

    #region Firebase Functionalities

    public void CreateUser( string email, string password)
    {
       
        errorText.text = "Creating ...";

        if (email == "")
            localSignUp = SignUpStates.No_Name;

        else
        {
            db.Collection("UserDetails").Document(email).GetSnapshotAsync().ContinueWith(task =>
            {
                if (task.IsCompleted)
                {
                    DocumentSnapshot snapshot = task.Result;
                    if (snapshot.Exists)
                    {
                        userNameExists = true;
                        localSignUp = SignUpStates.Name_Mismatched;

                    }
                    else
                    {
                        try
                        {
                            firebaseAuth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(t =>
                            {
                                if (t.IsCanceled)
                                {
                                    localSignUp = SignUpStates.Error;
                                }
                                else if (t.IsFaulted)
                                {
                                    if (t.Exception.ToString().Contains("The email address is badly formatted"))
                                    {
                                        localSignUp = SignUpStates.Wrong_Email_Format;

                                    }
                                    else if (t.Exception.ToString().Contains(" The given password is invalid"))
                                    {
                                        localSignUp = SignUpStates.Password_Not_Strong;

                                    }
                                    else
                                    {
                                        localSignUp = SignUpStates.Error;
                                    }

                                }

                                else
                                {
                                    CreateUserOnFirestore(t.Result.UserId, email);

                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e);
                        }
                    }
                }


            });
            
        }
    }

    public void SignInUser(string email, string password)
    {
       
        errorText.text = "Logging In...";

        try
        {
            firebaseAuth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
            {
                if (task.IsCanceled)
                {
                    localSignIn = SignInStates.None;
                }
                else if (task.IsFaulted)
                {
                    localSignIn = SignInStates.Other;
                }
                else
                {
                    CheckForExistingUsers(task.Result.UserId);
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    public void ResetPassword(string emailAddress)
    {

        errorText.text = "Sending...";

        try
        {
            firebaseAuth.SendPasswordResetEmailAsync(emailAddress).ContinueWith(task =>
            {
                if (task.IsCanceled)
                {
                    localSetNewPassword = SetNewPassword.Other;
                }
                else if (task.IsFaulted)
                {
                    localSetNewPassword = SetNewPassword.Other;
                }

                else
                {
                    localSetNewPassword = SetNewPassword.Success;
                }
            });
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    public void CheckForExistingUsers(string userUniqeId)
    {

        db.Collection("UserDetails").WhereEqualTo("uniqueUserID", userUniqeId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)

            {
                foreach (DocumentSnapshot snapshot in task.Result.Documents)
                {
                    try
                    {
                        UserDetails existingPlayer = snapshot.ConvertTo<UserDetails>();

                        
                        GameData.metaData = existingPlayer.metaData;
                        GameData.elementUpdated = true;
                        PlayerPrefs.SetString("UserUniqueId", userUniqeId);
                        PlayerPrefs.Save();



                        localSignIn = SignInStates.Success;

                    }
                    catch (Exception e)
                    {
                      
                    }

                }
            }
            else
            {
                localSignIn = SignInStates.Other;
            }



        });
    }

    public void CreateUserOnFirestore(string userId, string userEMail)
    {
        UserDetails userDetails = new UserDetails
        {
            uniqueUserID = userId,
            metaData = SearlizedMetaData()
           
        };
        db.Collection("UserDetails").Document(userEMail).SetAsync(userDetails).ContinueWith(task =>
        {

            if (task.IsCompleted)
            {

                try
                {
                    this.userId = userEMail;
                    uEmail = userEMail;
                    uId = userId;
                    //SetPlayerNameAndImage(uName);
                    localSignUp = SignUpStates.Success;
                }
                catch (Exception e)
                {
                    
                }
            }
            else
            {

                localSignUp = SignUpStates.Error;


            }


        });
    }

    public void UpdateValues()
    {


        if (PlayerPrefs.GetString("UserUniqueId") != null)
        {
            if (PlayerPrefs.GetString("UserUniqueId") != "")
            {


                UserDetails userDetails = new UserDetails
                {
                    uniqueUserID = PlayerPrefs.GetString("UserUniqueId"),
                    metaData = PlayerPrefs.GetString("currentUser").ToString()

                };
                db.Collection("UserDetails").Document(PlayerPrefs.GetString("userId")).SetAsync(userDetails).ContinueWith(task =>
                {
                    if (task.IsCompleted)
                    {

                    }
                    else
                    {



                    }


                });
            }
        }
    }

    public void SetPlayerNameAndImage(string playerName/*, string imageUrl*/)
    {
       FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user != null)
        {
           UserProfile profile = new UserProfile
            {
                DisplayName = playerName,
                //PhotoUrl = new System.Uri(imageUrl),
            };
            user.UpdateUserProfileAsync(profile).ContinueWith(task => {
                if (task.IsCanceled)
                {
                    
                    return;
                }
                if (task.IsFaulted)
                {
                  
                    return;
                }

                Debug.Log("User profile updated successfully.");
            });
        }
    }

    #endregion

    #region Functionalities Callback

    public void LoggedIn(string userName)
    {
        AudioTheme.instance.ButtonClickSound();
        PlayerScript.Instance.signInScreen.SetActive(false);
        PlayerScript.Instance.signUpScreen.SetActive(false);
        PlayerScript.Instance.userName.text = userName;
        PlayerScript.Instance.characterSelectionScreen.SetActive(true);
        PlayerPrefs.SetString("UserName", userName);
        PlayerPrefs.Save();
    }
    public void Toggle(int val) {
        AudioTheme.instance.ButtonClickSound();
        this.value = val;
    }
    public void SelectCharacter() {
        AudioTheme.instance.ButtonClickSound();
        PlayerPrefs.SetString("SignedIn", "Yes");
        PlayerScript.Instance.characterSelectionScreen.SetActive(false);
        switch (value) {
            case 0:
                PlayerPrefs.SetString("Female", "Yes");
                PlayerPrefs.SetString("Male", "No");
                PlayerScript.Instance.playerAvatarFemale.SetActive(true);
                PlayerScript.Instance.playerAvatarFemale.GetComponent<Animator>().SetLayerWeight(1, 0);
                
                break;
            case 1:
                PlayerPrefs.SetString("Male", "Yes");
                PlayerPrefs.SetString("Female", "No");
                PlayerScript.Instance.playerAvatarMale.SetActive(true);
                PlayerScript.Instance.playerAvatarMale.GetComponent<Animator>().SetLayerWeight(1, 0);
                

                break;
        }

       
       
        
    }
    public Data DeSearlizedMetaData() {

     return( JsonUtility.FromJson<Data>(GameData.metaData)); 

    }
    public string SearlizedMetaData() {

        Data data = new Data();
        return (JsonUtility.ToJson(data));
    }

    #endregion
}


[FirestoreData]
public struct UserDetails{

  
    [FirestoreProperty]
    public string uniqueUserID { get; set; }

    [FirestoreProperty]
    public string metaData { get; set; }

}
[FirestoreData]
public struct LeaderBoardData
{

    [FirestoreProperty]
    public string userName { get; set; }

    [FirestoreProperty]
    public string metaData { get; set; }

}

public class Data
{

    public string userName;
    public string userLevel;
    public string coinsCount;
    public string gemsCount;
    public string userXPs;


    public Data()
    {

        this.userName = GameData.userName;
        this.userLevel = GameData.userLevel;
        this.coinsCount = GameData.coinsCount;
        this.gemsCount = GameData.gemsCount;
        this.userXPs = GameData.userXPs;
    }


}