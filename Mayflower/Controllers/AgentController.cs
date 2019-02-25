using Alphareds.Module.AgentController;
using Alphareds.Module.Common;
using Alphareds.Module.CommonController;
using Alphareds.Module.Model;
using Alphareds.Module.Model.Database;
using Ionic.Zip;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace Mayflower.Controllers
{
    [Authorize]
    //[Filters.ControllerAccess.AllowOn(Localhost = true, Staging = true, Production = true)]
    [Filters.ControllerAccess.CheckOnConfiguration(AppKey = "EnableB2B")]
    public class AgentController : Controller
    {
        private MayFlower db = new MayFlower();
        private Logger logger = LogManager.GetCurrentClassLogger();

        public enum ManageMessageId
        {
            RegisterSuccess,
            RegisterFail
        }

        public enum MessageId
        {
            SuccessCreate,
            SuccessSendResetMail,
            SuccessReset,
            Fail
        }

        #region CheckInfoAvailability
        //JSON Check CompanyName
        [AllowAnonymous]
        public JsonResult IsCompanyNameAvailable(string CompanyName)
        {
            return Json(!(db.UserDetails.Any(x => x.CompanyName == CompanyName)), JsonRequestBehavior.AllowGet);
        }

        //JSON Check Organization Email
        [AllowAnonymous]
        public JsonResult IsEmailAvailable(string CompanyEmail, string Email)
        {
            MayFlower db = new MayFlower();
            return Json(!(db.Organizations.Any(x => x.Email == CompanyEmail || (Email != null && Email != "" && x.Email == Email))), JsonRequestBehavior.AllowGet);
        }

        #endregion

        [AllowAnonymous]
        public ActionResult Login(string returnUrl, string loginerror = "")
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.UserNotAuthorized = loginerror;
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Mayflower.General.AcceptCustomButton(Name = "btnLogin", Value = "Login")]
        public ActionResult Login(AgentLoginModels model, string returnUrl, bool rememberMe = false)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            MayFlower db = new MayFlower();
            Mayflower.General.LoginClass login = new General.LoginClass(model.UserLoginID, model.Pwd, true, db);
            var userData = login.UserData;

            if ((userData.IsValidUser && userData.IsActive))
            {
                Response.SetAuthCookie(userData.UserId.ToString(), rememberMe, userData);

                if (userData.IsProfileActive)
                {
                    Alphareds.Module.MemberController.MemberServiceController.UpdateLoginDate(login.User, db);
                }
                else if (!userData.IsProfileActive)
                {
                    // Redirect to profile page, for user request activation email.
                    //return RedirectToAction("ManageProfile", "Member");
                }

                Session.Remove("RequestLoginURL");

                return RedirectToAction("Index", "Home");
            }
            else
            {
                string errorMsg = string.Empty;

                if (userData.UserId != 0 && !userData.IsActive)
                { errorMsg = "Account disabled, please contact our customer services"; }
                else if (userData.UserId != 0 && userData.IsActive && !userData.IsProfileActive)
                { errorMsg = "Account inactive, please confirm your account activated first"; }
                else { errorMsg = "Incorrect Trading ID/ Username & Password"; }
                TempData["loginerr"] = errorMsg;
                if (returnUrl != null && string.IsNullOrWhiteSpace(errorMsg))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("Login", "Agent", new { loginerror = errorMsg, returnUrl });
                }
            }
        }

        /// <summary>
        /// Agent Profile Page Display       
        public ActionResult AgentManageProfile()
        {
            return View();
        }

        [HttpPost]
        public ActionResult agentManageProfile()
        {
            return View();
        }
        /// </summary>
        /// <returns></returns>

        [AllowAnonymous]
        public ActionResult AgentResetMailSend()
        {
            return View();
        }

        [AllowAnonymous]
        public ActionResult AgentResetPasswordSuccess()
        {
            return View();
        }

        [NoCache]
        [AllowAnonymous]
        public ActionResult AgentResetPassword()
        {

            return View();
        }

        [NoCache]
        [HttpPost]
        [AllowAnonymous]
        [Mayflower.General.AcceptCustomButton(Name = "Reset", Value = "Password")]
        public ActionResult AgentResetPassword(AgentPasswordResetModel model, string email)
        {
            if (ModelState.IsValid)
            {
                UserDetail user = db.UserDetails.FirstOrDefault(x => x.User.Email == email);

                if (user != null)
                {
                    var query = "EXEC Users.usp_UserResetLinkStatus @UserID";
                    db.Database.ExecuteSqlCommand(query, new SqlParameter("@UserID", user.UserID));

                    int TokenValidateHour = 0;
                    int.TryParse(Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("TokenValidateHour"), out TokenValidateHour);

                    string token = GenerateResetPasswordToken(user.UserID, user.User.OrganizationID, TokenValidateHour);
                    var EncToken = Mayflower.General.CustomizeBaseEncoding.CodeBase64(token);

                    Hashtable ht = new Hashtable();
                    var url = Url.Action("AgentMailResetPassword", "Agent", new { token = EncToken }, Request.Url.Scheme);
                    ht.Add("<#UserName>", user.FirstName);
                    ht.Add("<#ResetURL>", url);
                    bool successSend = CommonServiceController.SendEmail(user.User.Email, "Forgot password? Mayflower is here to help", Core.getMailTemplate("passwordresetlink", ht));

                    if (!successSend)
                    {
                        ModelState.AddModelError("errmsg", "Fail to deliver mail to your inbox, please try again later.");
                        return View(model);
                    }
                }
                // As Iry ARS Business Analysis requested, if mail not exist just inform user email not exist.
                return RedirectToAction("AgentResetMailSend", "Agent", new { Message = MessageId.SuccessSendResetMail });
            }
            return View(model);
        }


        [NoCache]
        [AllowAnonymous]
        public ActionResult AgentMailResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            PasswordResetToken requestInfo = DecodeResetPasswordToken<PasswordResetToken>(token);

            if (requestInfo != null)
            {
                bool isValidRequest = requestInfo.RequestUTCDate.AddHours((double)requestInfo.TokenValidateHour) <= DateTime.UtcNow ? false : true;
                var requestUser = db.Users.FirstOrDefault(x => x.UserID == requestInfo.UserID && x.OrganizationID == requestInfo.OrganizationID);

                if (!requestUser.IsResetLinkUsed && isValidRequest)
                {
                    ViewBag.ResetPasswordToken = token;
                    return View();
                }
                else
                {
                    return RedirectToAction("Login", "Agent", new { status = "link-expired" });
                }
            }
            return RedirectToAction("Login", "Agent", new { status = "invalid token" });
        }

        [NoCache]
        [HttpPost]
        [AllowAnonymous]
        public ActionResult AgentMailResetPassword(AgentLocalPasswordModel model, string token)
        {
            if (ModelState["OldPassword"].Errors != null) { ModelState["OldPassword"].Errors.Clear(); }

            if (ModelState.IsValid)
            {
                PasswordResetToken requestInfo = DecodeResetPasswordToken<PasswordResetToken>(token);
                if (requestInfo != null)
                {
                    bool isValidRequest = requestInfo.RequestUTCDate.AddHours((double)requestInfo.TokenValidateHour) <= DateTime.UtcNow ? false : true;
                    var requestUser = db.Users.FirstOrDefault(x => x.UserID == requestInfo.UserID && x.OrganizationID == requestInfo.OrganizationID);

                    if (!requestUser.IsResetLinkUsed && isValidRequest)
                    {
                        using (var dbContextTransaction = db.Database.BeginTransaction())
                        {
                            try
                            {
                                // As discussion from Kevin, Iry, set profile activated while reset password.
                                var query = "UPDATE Users.Users SET IsProfileActivated = @IsProfileActivated WHERE UserID = @UserID";
                                db.Database.ExecuteSqlCommand(query,
                                    new SqlParameter("UserID", requestUser.UserID),
                                    new SqlParameter("IsProfileActivated", true));

                                string PasswordSalt = Alphareds.Module.Common.Core.GeneratePasswordSalt();
                                var sql = "EXEC Users.usp_UserLoginPwdUpdate @UserID, @Pwd, @PwdSalt, @PreviousPwd, @PreviousPwdSalt, @ModifiedByID";
                                db.Database.ExecuteSqlCommand(sql,
                                    new SqlParameter("UserID", requestUser.UserID),
                                        new SqlParameter("Pwd", Core.Encrypt(model.NewPassword, PasswordSalt)),
                                        new SqlParameter("PwdSalt", PasswordSalt),
                                        new SqlParameter("PreviousPwd", requestUser.Pwd),
                                        new SqlParameter("PreviousPwdSalt", requestUser.PwdSalt),
                                        new SqlParameter("ModifiedByID", GetCurrentUserId));

                                #region Send mail to notice user password changed.
                                Hashtable ht = new Hashtable();
                                ht.Add("<#UserName>", requestUser.FullName);
                                ht.Add("<#Email>", requestUser.Email);
                                ht.Add("<#Password>", model.NewPassword);
                                ht.Add("<#LoginURL>", Url.Action("Login", "Account", new { @ref = "mail" }, Request.Url.Scheme));
                                ht.Add("<#DateChange>", DateTime.Now.ToString("dd MMM yyyy hh:mm tt"));

                                CommonServiceController.SendEmail(requestUser.Email, "Mayflower - Password Changed Notice",
                                    Core.getMailTemplate("resetpasswordnotice", ht));
                                #endregion

                                dbContextTransaction.Commit();
                            }
                            catch (Exception ex)
                            {
                                dbContextTransaction.Rollback();
                                Logger logger = LogManager.GetCurrentClassLogger();
                                logger.Error(ex);
                                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                            }
                        }
                    }
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                }
                return RedirectToAction("AgentResetPasswordSuccess", "Agent");
            }
            return View(model);
        }

        private string GenerateResetPasswordToken(int userid, int orgid, int validateHour)
        {
            var serializeJson = JsonConvert.SerializeObject(new { UserID = userid, OrganizationID = orgid, RequestUTCDate = DateTime.UtcNow, TokenValidateHour = validateHour });

            return serializeJson;
        }

        private T DecodeResetPasswordToken<T>(string encrypedToken)
        {
            string dectoken = Mayflower.General.CustomizeBaseEncoding.DeCodeBase64(encrypedToken);
            return dectoken != "" ? JsonConvert.DeserializeObject<T>(dectoken) : default(T);
        }

        [NoCache]
        public ActionResult AgentCardView()
        {
            var model = populateAgentProfileEditModel()
                .FirstOrDefault(x => x.UserID == GetCurrentUserId);

            if (model == null)
            {
                return HttpNotFound();
            }
            return View(model);
        }

        /// <summary>
        /// Page For Agent to Update the Information       
        public ActionResult AgentUpdateProfile()
        {
            var model = populateAgentProfileEditModel()
                .FirstOrDefault(x => x.UserID == GetCurrentUserId);

            if (model == null)
            {
                return HttpNotFound();
            }
            return View(model);
        }

        [HttpPost]
        public ActionResult agentUpdateProfile()
        {
            return View();
        }
        /// </summary>
        /// <returns></returns>

        [AllowAnonymous]
        public ActionResult AgentRegister()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            var db = new MayFlower();
            AgentRegisterModels model = new AgentRegisterModels();

            List<Temp_RegistationSelectionType> selectionList = db.Temp_RegistationSelectionType.ToList();

            model.GDS = selectionList.Select(x => new AgentGDS
            {
                SelectionTypeID = x.SelectionTypeID.ToString(),
                SelectionType = x.SelectionType,
                SelectionName = x.SelectionName,
                SelectionDescription = x.SelectionDescription
            }).Where(x => x.SelectionType == "GDS").ToList();

            model.Business = selectionList.Select(x => new AgentBusiness
                {
                    SelectionTypeID = x.SelectionTypeID.ToString(),
                    SelectionType = x.SelectionType,
                    SelectionName = x.SelectionName,
                    SelectionDescription = x.SelectionDescription
                }).Where(x => x.SelectionType == "LineOfBusiness").ToList();

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        [Mayflower.General.AcceptCustomButton(Name = "btnSelfServiceCreateOrganization", Value = "Create")]
        public ActionResult AgentRegister(AgentRegisterModels model)
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            SqlCommand command = new SqlCommand();
            //try
            //{
            //    AgentServiceController.InsertTempAgentOrganization(model, command);
            //    AgentServiceController.InsertTempOrganizationAgentContactPerson(model, command);
            //    command.Transaction.Commit();
            //}
            //catch (Exception ex)
            //{
            //    if (command.Transaction != null)
            //        command.Transaction.Rollback();
            //    throw ex;
            //}


            if (model.CompanyType == "FRELCR")
            {
                ModelState["BusinessRegisterationNo"].Errors.Clear();
                ModelState["KPLNo"].Errors.Clear();
                //ModelState["ContactDetails[1].FirstName"].Errors.Clear();
                //ModelState["ContactDetails[1].LastName"].Errors.Clear();
                //ModelState["ContactDetails[1].Email"].Errors.Clear();
                //ModelState["ContactDetails[1].OfficePhone"].Errors.Clear();
                //ModelState["ContactDetails[1].MobilePhone"].Errors.Clear();
                //ModelState["ContactDetails[1].Designation"].Errors.Clear();
                ModelState["Supporting.BusinessRegistration"].Errors.Clear();
            }
            else
            {
                ModelState["ICNumber"].Errors.Clear();
            }
            //var error = ModelState.GetModelErrors();
            if (ModelState.IsValid)
            {
                try
                {
                    int agentId = AgentServiceController.InsertTempAgentOrganization(model, command);
                    model.ContactDetails.AgentID = agentId.ToString();
                    AgentServiceController.InsertTempOrganizationAgentContactPerson(model.ContactDetails, command);
                    if (model.ContactPerson2.FirstName != null && model.ContactPerson2.LastName != null && model.ContactPerson2.Email != null)
                    {
                        model.ContactPerson2.AgentID = agentId.ToString();
                        AgentServiceController.InsertTempOrganizationAgentContactPerson2(model.ContactPerson2, command);
                    }
                    string regDate = DateTime.Today.ToString("yyyyMMdd");
                    var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    bool supportDoc = true;
                    model.AgentID = agentId.ToString();

                    foreach (var BusinessdSelection in model.Business)
                    {
                        int id = -1;
                        int.TryParse(BusinessdSelection.SelectionTypeID, out id);
                        if (id != -1)
                        {
                            var dbRegistationTempData1 = db.Temp_RegistationSelectionType.FirstOrDefault(x => x.SelectionTypeID == id);
                            BusinessdSelection.AgentID = model.AgentID;
                            BusinessdSelection.SelectionDescription = dbRegistationTempData1.SelectionDescription;
                            BusinessdSelection.SelectionName = dbRegistationTempData1.SelectionName;
                            BusinessdSelection.CreatedByID = dbRegistationTempData1.CreatedByID;
                            BusinessdSelection.ModifiedByID = dbRegistationTempData1.ModifiedByID;
                            AgentServiceController.InsertAgentMultipleSelectionInsert(BusinessdSelection, command);
                        }
                    }

                    foreach (var GdsSelection in model.GDS)
                    {
                        int id = -1;
                        int.TryParse(GdsSelection.SelectionTypeID, out id);
                        if (id != -1)
                        {
                            var dbRegistationTempData = db.Temp_RegistationSelectionType.FirstOrDefault(x => x.SelectionTypeID == id);
                            GdsSelection.AgentID = model.AgentID;
                            GdsSelection.SelectionDescription = dbRegistationTempData.SelectionDescription;
                            GdsSelection.SelectionName = dbRegistationTempData.SelectionName;
                            GdsSelection.CreatedByID = dbRegistationTempData.CreatedByID;
                            GdsSelection.ModifiedByID = dbRegistationTempData.ModifiedByID;

                            AgentServiceController.InsertAgentMultipleSelectionInsert(GdsSelection, command);
                        }
                    }
 
                    foreach (var EmergencyDetail in model.EmergencyContact)
                    {
                        EmergencyDetail.IsEmergencyContact = true;
                        EmergencyDetail.AgentID = model.AgentID;
                        AgentServiceController.InsertAgentEmergencyContactPerson(EmergencyDetail, command);
                    }

                    if (model.Supporting.KPL != null && model.Supporting.KPL.ContentLength > 0)
                    {
                        var KplDocument = model.Supporting;
                        var fileName = model.CompanyType.IsStringSame("FRELCR") ? model.OrganizationName.Replace(" ", "") + "_IC_" + regDate + Path.GetExtension(model.Supporting.KPL.FileName).ToLower() : model.OrganizationName.Replace(" ", "") + "_KPL_" + regDate + Path.GetExtension(model.Supporting.KPL.FileName).ToLower();
                        var path = model.CompanyType.IsStringSame("FRELCR") ? HttpContext.Server.MapPath("~/B2B_Agent_Doc/IC") : HttpContext.Server.MapPath("~/B2B_Agent_Doc/KPL");
                        var checkKplExtension = Path.GetExtension(model.Supporting.KPL.FileName).ToLower();
                        KplDocument.AgentID = model.AgentID;
                        KplDocument.DocumentPath = model.CompanyType.IsStringSame("FRELCR") ? "~/B2B_Agent_Doc/IC" + "/" + fileName : "~/B2B_Agent_Doc/KPL" + "/" + fileName;
                        KplDocument.DocumentName = fileName;
                        KplDocument.DocumentDescription = model.CompanyType.IsStringSame("FRELCR") ? model.OrganizationName + " IC Document" : model.OrganizationName + " KPL Document";

                        if (!allowedExtensions.Contains(checkKplExtension))
                        {
                            supportDoc = false;
                            TempData["noticeKPL"] = "Invalid File Format.";
                        }
                        else
                        {
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }

                            string savePath = Path.Combine(path, fileName);
                            KplDocument.KPL.SaveAs(savePath);

                            AgentServiceController.InsertRegistrationAgentSupportDocument(KplDocument, command);
                        }
                    }

                    if (model.Supporting.BusinessRegistration != null && model.Supporting.BusinessRegistration.ContentLength > 0)
                    {
                        var BusinessDocument = model.Supporting;
                        var fileName = model.OrganizationName.Replace(" ", "") + "_BusinessRegistration_" + regDate + Path.GetExtension(model.Supporting.BusinessRegistration.FileName).ToLower();
                        var path = HttpContext.Server.MapPath("~/B2B_Agent_Doc/BusinessRegistration");
                        var checkBusinessExtension = Path.GetExtension(model.Supporting.BusinessRegistration.FileName).ToLower();
                        BusinessDocument.AgentID = model.AgentID;
                        BusinessDocument.DocumentPath = "~/B2B_Agent_Doc/BusinessRegistration" + "/" + fileName;
                        BusinessDocument.DocumentName = fileName;
                        BusinessDocument.DocumentDescription = model.OrganizationName + " Business Registration Document";

                        if (!allowedExtensions.Contains(checkBusinessExtension))
                        {
                            supportDoc = false;
                            TempData["noticeBusiness"] = "Invalid File Format.";
                        }
                        else
                        {
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }

                            string savePath = Path.Combine(path, fileName);
                            model.Supporting.BusinessRegistration.SaveAs(savePath);

                            AgentServiceController.InsertRegistrationAgentSupportDocument(BusinessDocument, command);
                        }
                    }

                    if (model.Supporting.Borang9 != null && model.Supporting.Borang9.ContentLength > 0)
                    {
                        var Borang9Document = model.Supporting;
                        var fileName = model.CompanyType.IsStringSame("FRELCR") ? model.OrganizationName.Replace(" ", "") + "_SSM_" + regDate + Path.GetExtension(model.Supporting.Borang9.FileName).ToLower() : model.OrganizationName.Replace(" ", "") + "_Borang9_" + regDate + Path.GetExtension(model.Supporting.Borang9.FileName).ToLower();
                        var path = model.CompanyType.IsStringSame("FRELCR") ? HttpContext.Server.MapPath("~/B2B_Agent_Doc/SSM") : HttpContext.Server.MapPath("~/B2B_Agent_Doc/Borang9");
                        var checkBorang9Extension = Path.GetExtension(model.Supporting.Borang9.FileName).ToLower();
                        Borang9Document.AgentID = model.AgentID;
                        Borang9Document.DocumentPath = model.CompanyType.IsStringSame("FRELCR") ? "~/B2B_Agent_Doc/SSM" + "/" + fileName : "~/B2B_Agent_Doc/Borang9" + "/" + fileName;
                        Borang9Document.DocumentName = fileName;
                        Borang9Document.DocumentDescription = model.CompanyType.IsStringSame("FRELCR") ? model.OrganizationName + " SSM Document" : model.OrganizationName + " Borang9 Document";

                        if (!allowedExtensions.Contains(checkBorang9Extension))
                        {
                            supportDoc = false;
                            TempData["noticeBorang9"] = "Invalid File Format.";
                        }
                        else
                        {
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }

                            string savePath = Path.Combine(path, fileName);
                            model.Supporting.Borang9.SaveAs(savePath);

                            AgentServiceController.InsertRegistrationAgentSupportDocument(Borang9Document, command);
                        }
                    }
                    command.Transaction.Commit();
                    GenerateActivateMail(model.OrganizationName, model.CompanyEmail);
                    //dbContextTransaction.Commit();

                    if (!supportDoc)
                    {
                        //Bind back business and gds
                        model = PopulateBusinessGds(model);
                        return View(model);
                    }
                    else
                    {
                        return RedirectToAction("SuccessRegister", new { Message = ManageMessageId.RegisterSuccess });
                    }
                }
                catch (Exception ex)
                {
                    if (command != null && command.Transaction != null)
                    {
                        command.Transaction.Rollback();
                    }

                    //if (dbContextTransaction != null)
                    //{
                    //    dbContextTransaction.Rollback();
                    //}

                    StringBuilder sb = new StringBuilder();
                    //sb.AppendLine();
                    //sb.AppendLine("OrganizationName    : " + model.CompanyName);
                    //sb.AppendLine("Email               : " + model.Email);
                    //sb.AppendLine("Password            : " + model.ConfirmPassword);
                    //sb.AppendLine("FirstName           : " + model.FirstName);
                    //sb.AppendLine("LastName            : " + model.LastName);
                    //sb.AppendLine("PassportName        : " + model.PassportName);
                    //sb.AppendLine("MobilePhone         : " + model.PrimaryPhone);
                    //sb.AppendLine("CountryCode           : " + model.CountryCode);
                    //sb.AppendLine("IsActive            : " + model.IsActive);

                    Logger logger = LogManager.GetCurrentClassLogger();
                    logger.Error(ex, sb.ToString(), null);

                    return RedirectToAction("Register", new { Message = ManageMessageId.RegisterFail });
                    //throw ex;
                }
            }

            //Bind back business and gds
            model = PopulateBusinessGds(model);
            return View(model);
        }

        private Tuple<bool, string, string, bool, string, string, bool> IsValidActiveAgent(string UserLoginId, string Password)
        {
            string OrganizationID = string.Empty, OrganizationName = string.Empty;
            bool IsCorrect = false;
            bool IsValid = false;
            bool IsActive = false;
            //bool _IsAgent = false;
            //bool _IsFreelancer = false;
            string roleName = string.Empty;
            string UserId = string.Empty;
            string FirstName = string.Empty;
            bool IsProfileActive = false;

            MayFlower db = new MayFlower();

            //var User = db.Users.FirstOrDefault(u => u.UserLoginID == tradingID);
            var user = db.Users.FirstOrDefault(u => u.UserLoginID == UserLoginId && (u.UserTypeCode == "FRELCR" || u.UserTypeCode == "AGT"));
            // _IsAgent = user.UserTypeCode == "AGT" ? true : false;

            if (user != null)
            {
                //var UserRoles = db.Roles.Where(ui => ui.RoleID == User.UserID);
                //var chkAdmin = UserRoles.FirstOrDefault(x => x.Role.RoleName.Contains("company"));
                var Organization = user.Organization;
                var UserDetail = user.UserDetails.FirstOrDefault();
                //var UserRoles = db.UsersInRoles.FirstOrDefault(ui => ui.UserId == User.UserID);
                //var UserRolesInfo = db.Roles.FirstOrDefault(x => x.RoleID == UserRoles.RoleId);
                string password1 = Core.Encrypt(Password, user.PwdSalt);

                if (password1 == user.Pwd)
                {
                    IsCorrect = password1 == user.Pwd;
                    roleName = user.UserType.UserType1;
                    OrganizationID = user.OrganizationID < 0 ? OrganizationID : user.OrganizationID.ToString().Trim();
                    OrganizationName = string.IsNullOrEmpty(Organization.OrganizationName) ? OrganizationName : Organization.OrganizationName.ToString().Trim();
                    IsActive = Organization.IsActive;
                    //IsActive = User.IsActive && User.IsProfileActivated;
                    //_IsAgent = user.UserTypeCode == "AGT" ? true : false;
                    //_IsFreelancer = user.UserTypeCode == "FRELCR" ? true : false;
                    UserId = user.UserID.ToString();
                    FirstName = UserDetail.FirstName;
                    //IsComapnyAdmin = chkAdmin != null ? true : false;
                    IsProfileActive = user.IsProfileActivated;
                }
            }


            //make sure bothe userid and username have value in it, if not return not valid 
            IsValid = (!string.IsNullOrEmpty(OrganizationID) && !string.IsNullOrEmpty(OrganizationName) && !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(FirstName)) ? true : IsValid;

            return Tuple.Create(IsValid, OrganizationID, FirstName, IsActive, roleName, UserId, IsProfileActive);
        }

        // GET: return view success activated
        [AllowAnonymous]
        public ActionResult SuccessValidate()
        {
            var email = Request.QueryString["Email"];
            Temp_RegistrationAgentOrganization org = db.Temp_RegistrationAgentOrganization.FirstOrDefault(x => x.Email == email);

            if (org == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            if (org.IsApproved)
            {
                return View("SuccessValidate", new { IsApproved = "true" });
            }

            return View();
        }

        // GET: return view success activated
        [AllowAnonymous]
        public ActionResult SuccessRegister()
        {
            var success = Request.QueryString["Message"];

            if (success != null && success == "RegisterSuccess")
            {
                return View();
            }

            return View("Index");
        }

        /// GET: /Member/ActivateByEmailLink
        [AllowAnonymous]
        public ActionResult ActivateByEmailLink(string Token, string Email)
        {
            int agentID = -1;
            bool success = int.TryParse(Mayflower.General.CustomizeBaseEncoding.DeCodeBase64(Token), out agentID);

            if (success)
            {
                Temp_RegistrationAgentOrganization org = db.Temp_RegistrationAgentOrganization.FirstOrDefault(u => u.RegistrationAgentID == agentID);

                if (org != null)
                {
                    if (org.IsEmailVerify == false && org.Email == Email)
                    {
                        try
                        {
                            if (org.OrganizationTypeCode == "FRELCR") {
                                org.KPLNo = org.KPLNo ?? "-";
                            }
                            org.IsEmailVerify = true;
                            db.SaveChanges();

                            AgentRegisterData agentData = new AgentRegisterData
                        {
                            OrganizationID = org.RegistrationAgentID.ToString(),
                            CompanyName = org != null ? org.OrganizationName : "undefined",
                            //IsActive = org.IsApproved,
                            IsEmailVerify = org.IsEmailVerify,
                        };

                            // Response.SetAuthCookie(agentData.OrganizationID, false, agentData);
                            //GenerateVerifiedMail(org.OrganizationName, Email);
                            SendRegisterNoticeEmail(org.OrganizationName, org.Email);
                            return RedirectToAction("SuccessValidate", "Agent", new { Email = org.Email.ToLower() });
                        }
                        catch (Exception ex)
                        {
                            logger.Fatal(ex);
                            //return RedirectToAction("SuccessValidate", "Agent", new { Email = org.Email.ToLower() });
                        }



                    }
                }
            }

            // 2016/05/26 - Any failure return to home page.
            return RedirectToAction("Index", "Home");
        }

        public void SendRegisterNoticeEmail(string OrganizationName, string RegisteredEmail)
        {
            //retreive new inserted Traveller Admin
            Temp_RegistrationAgentOrganization org = db.Temp_RegistrationAgentOrganization.OrderByDescending(x => x.CreatedDate).FirstOrDefault(x => x.Email == RegisteredEmail && x.OrganizationName == OrganizationName);
            AgentRegisterModels model = new AgentRegisterModels();

            if (org != null || true)
            {
                //string RegistrationAgentID = Mayflower.General.CustomizeBaseEncoding.CodeBase64(org.RegistrationAgentID.ToString());                
                var doc = db.Temp_RegistrationAgentSupportDocument.Where(x => x.RegistrationAgentID == org.RegistrationAgentID);

                string RegistrationAgentID = Mayflower.General.CustomizeBaseEncoding.CodeBase64(org.RegistrationAgentID.ToString());
                string emailaddress = Core.IsForStaging ? Core.GetAppSettingValueEnhanced("MayflowerB2BRegNoticeEmailStaging") : Core.GetAppSettingValueEnhanced("MayflowerB2BRegNoticeEmail");
                string registrationNo = org.RegistrationNo;
                string emailAddress = org.Email;
                string companyAddress = org.Address1 + " " + org.Address2 + " " + org.Address3 + " " + org.PostCode + " " + org.State + " " + org.City + " " + org.Country.Country1;
                var supportingDoc = model.Supporting;
                string savePath = "~/B2B_Agent_Doc/CompanyZipFile/";
                var absolutePath = HttpContext.Server.MapPath(savePath);
                string fileName = RegistrationAgentID + "_" + org.OrganizationName + ".zip";
                var saveToFilePath = absolutePath + fileName;

                if (!System.IO.Directory.Exists(absolutePath))
                {
                    System.IO.Directory.CreateDirectory(absolutePath);
                }

                using (ZipFile zip = new ZipFile())
                {
                    foreach (var _doc in doc)
                    {
                        zip.AddFile(HttpContext.Server.MapPath(_doc.DocumentPath), org.OrganizationName);
                    }
                    zip.Save(saveToFilePath);
                }

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", OrganizationName);
                ht.Add("<#RegistrationNo>", registrationNo);
                ht.Add("<#EmailAddress>", emailAddress);
                ht.Add("<#CompanyAddress>", companyAddress);
                ht.Add("<#Attachment>", savePath.Replace("~", "") + fileName);

                CommonServiceController.SendEmail(emailaddress, "New agent application received!", Core.getMailTemplate("b2bregnotice", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public void GenerateActivateMail(string OrganizationName, string RegisteredEmail)
        {
            //retreive new inserted Traveller Admin
            Temp_RegistrationAgentOrganization org = db.Temp_RegistrationAgentOrganization.OrderByDescending(x => x.CreatedDate).FirstOrDefault(x => x.Email == RegisteredEmail && x.OrganizationName == OrganizationName);
            if (org != null)
            {
                string RegistrationAgentID = Mayflower.General.CustomizeBaseEncoding.CodeBase64(org.RegistrationAgentID.ToString());
                var emailaddress = org.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", OrganizationName);
                ht.Add("<#UserID>", Url.Action("ActivateByEmailLink", "Agent", new { Token = RegistrationAgentID, Email = RegisteredEmail }));
                ht.Add("<#Token>", RegistrationAgentID);
                ht.Add("<#Email>", RegisteredEmail);


                CommonServiceController.SendEmail(emailaddress, "Mayflower needs you to validate your email address!", Core.getMailTemplate("agentactivemail", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public void GenerateVerifiedMail(string OrganizationName, string RegisteredEmail)
        {
            //retreive new inserted Traveller Admin
            Temp_RegistrationAgentOrganization org = db.Temp_RegistrationAgentOrganization.FirstOrDefault(x => x.Email == RegisteredEmail);

            if (org != null)
            {
                string registeredUserID = Mayflower.General.CustomizeBaseEncoding.CodeBase64(org.RegistrationAgentID.ToString());
                var emailaddress = org.Email.ToString();

                Hashtable ht = new Hashtable();
                ht.Add("<#UserName>", OrganizationName);
                ht.Add("<#Email>", RegisteredEmail);

                CommonServiceController.SendEmail(emailaddress, "You did it, buddy! Welcome aboard to the Mayflower community. ", Core.getMailTemplate("agentverified", ht));
            }
            else
            {
                throw new HttpException(404, "Not Found");
            }
        }

        public ActionResult Register(ManageMessageId? message)
        {
            StringBuilder builderSuccess = new StringBuilder();
            builderSuccess.Append("Your Registration to Mayflower is Successful!").AppendLine();
            builderSuccess.Append("A confirmation email will be sent shortly, kindly check your mailbox.").AppendLine();

            if (message == ManageMessageId.RegisterSuccess)
            {
                ViewBag.StatusMessage = builderSuccess.ToString();
            }
            else if (message == ManageMessageId.RegisterFail)
            {
                ViewBag.StatusMessage = MvcHtmlString.Create("We are very sorry but we seem to have encountered <br /> an issue with your registration." +
                    "<br/>Our administrator is looking into this as soon as possible." +
                    "<br/>We would greatly appreciate if you can try again later.");
            }
            else
            {
                ViewBag.StatusMessage = null;
            }

            return View();
        }

        private void usp_UserInsert_Member(MemberRegisterModels model)
        {
            int OrganizationID;
            Organization organization = db.Organizations.FirstOrDefault(o => o.OrganizationName == model.CompanyName);
            OrganizationID = organization != null ? organization.OrganizationID : 0;

            string PasswordSalt = Core.GeneratePasswordSalt();

            var query = string.Empty;
            query = "Exec Users.usp_UserInsert_Member @Pwd, @PwdSalt, @Email, @TitleCode, @FirstName, @LastName, @PassportName, @PrimaryPhoneCountryCode, @PrimaryPhone,";
            query += "@SecondaryPhoneCountryCode, @SecondaryPhone, @DOB, @IdentificationNumber, @AirlineCode, @FrequentFlyerNumber, @PassportNumber, @PassportExpiryDate,";
            query += "@PassportIssuingCountry, @Address1, @Address2, @City,	@PostalCode, @State, @Country, @CompanyName, @CompanyAddress1,";
            query += "@CompanyAddress2, @CompanyCity, @CompanyPostalCode, @CompanyState, @CompanyCountry, @CreatedByID, @ModifiedByID, @IsActive";

            db.Database.ExecuteSqlCommand(query,
                new SqlParameter("Pwd", !string.IsNullOrEmpty(model.Password) ? Core.Encrypt(model.Password, PasswordSalt) : (object)DBNull.Value),
                new SqlParameter("PwdSalt", !string.IsNullOrEmpty(PasswordSalt) ? PasswordSalt : (object)DBNull.Value),
                new SqlParameter("Email", !string.IsNullOrEmpty(model.Email) ? model.Email : (object)DBNull.Value),
                new SqlParameter("TitleCode", !string.IsNullOrEmpty(model.TitleCode) ? model.TitleCode : (object)DBNull.Value),
                new SqlParameter("FirstName", !string.IsNullOrEmpty(model.FirstName) ? model.FirstName.Trim() : (object)DBNull.Value),
                new SqlParameter("LastName", !string.IsNullOrEmpty(model.LastName) ? model.LastName.Trim() : (object)DBNull.Value),
                new SqlParameter("PassportName", !string.IsNullOrEmpty(model.PassportName) ? model.PassportName.Trim() : (object)DBNull.Value),
                new SqlParameter("PrimaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode1) ? model.PhoneCode1 : "NA"),
                new SqlParameter("PrimaryPhone", !string.IsNullOrEmpty(model.PrimaryPhone) ? model.PrimaryPhone : (object)DBNull.Value),
                new SqlParameter("SecondaryPhoneCountryCode", !string.IsNullOrEmpty(model.PhoneCode2) ? model.PhoneCode2 : "NA"),
                new SqlParameter("SecondaryPhone", !string.IsNullOrEmpty(model.SecondaryPhone) ? model.SecondaryPhone : (object)DBNull.Value),
                new SqlParameter("DOB", model.DOB == DateTime.MinValue ? (object)DBNull.Value : model.DOB),
                new SqlParameter("IdentificationNumber", !string.IsNullOrEmpty(model.IdentityNo) ? model.IdentityNo : (object)DBNull.Value),
                new SqlParameter("AirlineCode", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerAirlineCode : (object)DBNull.Value),
                new SqlParameter("FrequentflyerNumber", !string.IsNullOrEmpty(model.FrequentFlyerNumber) ? model.FrequentFlyerNumber : (object)DBNull.Value),
                new SqlParameter("PassportNumber", !string.IsNullOrEmpty(model.PassportNo) ? model.PassportNo : (object)DBNull.Value),
                new SqlParameter("PassportExpiryDate", model.PassportExpiryDate == null || model.PassportExpiryDate == DateTime.MinValue ? (object)DBNull.Value : model.PassportExpiryDate),
                new SqlParameter("PassportIssuingCountry", !string.IsNullOrEmpty(model.PassportIssuePlace) ? model.PassportIssuePlace : (object)DBNull.Value),
                new SqlParameter("Address1", !string.IsNullOrEmpty(model.Address1) ? model.Address1 : (object)DBNull.Value),
                new SqlParameter("Address2", !string.IsNullOrEmpty(model.Address2) ? model.Address2 : (object)DBNull.Value),
                new SqlParameter("City", !string.IsNullOrEmpty(model.City) ? model.City : (object)DBNull.Value),
                new SqlParameter("PostalCode", !string.IsNullOrEmpty(model.Postcode) ? model.Postcode : (object)DBNull.Value),
                new SqlParameter("State", !string.IsNullOrEmpty(model.AddressProvinceState) ? model.AddressProvinceState : (object)DBNull.Value),
                new SqlParameter("Country", !string.IsNullOrEmpty(model.CountryCode) ? model.CountryCode : (object)DBNull.Value),
                new SqlParameter("CompanyName", !string.IsNullOrEmpty(model.CompanyName) ? model.CompanyName : (object)DBNull.Value),
                new SqlParameter("CompanyAddress1", !string.IsNullOrEmpty(model.CompanyAddress1) ? model.CompanyAddress1 : (object)DBNull.Value),
                new SqlParameter("CompanyAddress2", !string.IsNullOrEmpty(model.CompanyAddress2) ? model.CompanyAddress2 : (object)DBNull.Value),
                new SqlParameter("CompanyCity", !string.IsNullOrEmpty(model.CompanyCity) ? model.CompanyCity : (object)DBNull.Value),
                new SqlParameter("CompanyPostalCode", !string.IsNullOrEmpty(model.CompanyPostcode) ? model.CompanyPostcode : (object)DBNull.Value),
                new SqlParameter("CompanyState", !string.IsNullOrEmpty(model.CompanyAddressProvinceState) ? model.CompanyAddressProvinceState : (object)DBNull.Value),
                new SqlParameter("CompanyCountry", !string.IsNullOrEmpty(model.CompanyAddressCountryCode) ? model.CompanyAddressCountryCode : (object)DBNull.Value),
                new SqlParameter("CreatedByID", GetCurrentUserId),
                new SqlParameter("ModifiedByID", GetCurrentUserId),
                new SqlParameter("IsActive", true));

            db.SaveChanges();
        }

        private IQueryable<AgentProfileEditModel> populateAgentProfileEditModel()
        {
            var model = (from a in db.Users
                         select new AgentProfileEditModel()
                         {
                             UserID = a.UserID,
                             Email = a.Organization.Email,
                             OrganizationID = a.OrganizationID,
                             OrganizationName = a.Organization.OrganizationName,
                             Address1 = a.Organization.Address1,
                             Address2 = a.Organization.Address2,
                             PostCode = a.Organization.PostCode,
                             City = a.Organization.City,
                             ProvinceState = a.Organization.ProvinceState,
                             CountryCode = a.Organization.CountryCode,
                             RegistrationNo = a.Organization.RegistrationNo,
                             TaxRegistrationNo = a.Organization.TaxRegistrationNo,
                             KPLNo = a.Organization.KPLNo,
                             OfficeNoCountryCode = a.Organization.OfficeNoCountryCode,
                             ContactNo1 = a.Organization.ContactNo1,
                             MobileNoCountryCode = a.Organization.MobileNoCountryCode,
                             ContactNo2 = a.Organization.ContactNo2,
                             FaxNoCountryCode = a.Organization.FaxNoCountryCode,
                             ContactNo3 = a.Organization.ContactNo3,
                         });

            return model;
        }

        private AgentRegisterModels PopulateBusinessGds(AgentRegisterModels model)
        {
            var db = new MayFlower();
            List<Temp_RegistationSelectionType> selectionList = db.Temp_RegistationSelectionType.ToList();

            model.GDS = selectionList.Select(x => new AgentGDS
            {
                SelectionTypeID = x.SelectionTypeID.ToString(),
                SelectionType = x.SelectionType,
                SelectionName = x.SelectionName,
                SelectionDescription = x.SelectionDescription
            }).Where(x => x.SelectionType == "GDS").ToList();

            model.Business = selectionList.Select(x => new AgentBusiness
            {
                SelectionTypeID = x.SelectionTypeID.ToString(),
                SelectionType = x.SelectionType,
                SelectionName = x.SelectionName,
                SelectionDescription = x.SelectionDescription
            }).Where(x => x.SelectionType == "LineOfBusiness").ToList();

            return model;
        }

        private int GetCurrentUserId
        {
            get
            {
                int userid = 0;
                int.TryParse(User.Identity.Name, out userid);
                return userid;
            }
        }
    }
}