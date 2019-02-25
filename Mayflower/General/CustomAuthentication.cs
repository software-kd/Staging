using Mayflower.Models;
using System;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
 
namespace Mayflower.CustomMembershipEF.Infrastructure
{
    //public class CustomAuthentication
    //{
    //}

    #region custom Login
     
    //interface ICustomPrincipal : IPrincipal
    //{
    //    int Id { get; set; }
    //    string FirstName { get; set; }
    //    string LastName { get; set; }
    //    string IsRole { get; set; }
    //}

    //public class CustomPrincipal : RoleProvider, ICustomPrincipal
    //{
    //    private CorpBookingContext db = new CorpBookingContext();

    //    public IIdentity Identity { get; private set; }

    //    public bool IsInRole(string rolename)
    //    {
    //        return false; 
    //        return Roles.IsUserInRole(rolename);
    //    }

    //    public CustomPrincipal(string email, string[] UserRoles)
    //    {
    //        this.Identity = new GenericIdentity(email);
    //        var principal = new GenericPrincipal(new GenericIdentity(email), UserRoles);
    //    }

    //    public int Id { get; set; }
    //    public string FirstName { get; set; }
    //    public string LastName { get; set; }
    //    public string IsRole { get; set; }

    //    Role Provider
    //    public override bool IsUserInRole(string UserId, string RoleId)
    //    {
    //        int id = string.IsNullOrEmpty(UserId) ? 0 : Convert.ToInt32(UserId);

    //        var user = db.Agents.SingleOrDefault(u => u.AgentID == id);
    //        if (user == null)
    //            return false;
    //        return true;
    //        return user.UserRoles != null && user.UserRoles.Select(u => u.Role).Any(r => r.RoleName == roleName);

    //    }

    //    public override string[] GetRolesForUser(string UserId)
    //    {
    //        int id = string.IsNullOrEmpty(UserId) ? 0 : Convert.ToInt32(UserId);

    //        var user = db.Agents.SingleOrDefault(u => u.AgentID == id);
    //        if (user == null)
    //            return new string[] { };
    //        return user.AgentRoleID == null ? new string[] { } : new string[] { user.AgentRoleID.ToString() };
    //    }

    //    public override string[] GetAllRoles()
    //    {
    //        return db.AgentRoles.Select(r => r.AgentRole1).ToArray();
    //    }

    //    #region not in use but have to declare due to abstract class
    //    public override string ApplicationName
    //    {
    //        get
    //        {
    //            throw new NotImplementedException();
    //        }
    //        set
    //        {
    //            throw new NotImplementedException();
    //        }
    //    }

    //    public override void CreateRole(string roleName)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override string[] GetUsersInRole(string roleName)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override void AddUsersToRoles(string[] usernames, string[] roleNames)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override string[] FindUsersInRole(string roleName, string usernameToMatch)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override bool RoleExists(string roleName)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    #endregion

    //}
    #endregion 
}