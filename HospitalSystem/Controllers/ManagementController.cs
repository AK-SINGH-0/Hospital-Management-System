using HospitalSystem.Data;
using HospitalSystem.Models;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;

namespace HospitalSystem.Controllers
{
    public class ManagementController : Controller
    {
        private HospitalSystem3Context db = new HospitalSystem3Context();

        // GET: Management
        public ActionResult Index() //login page
        {
            string userGuid = User.Identity.GetUserId();

            if (User.IsInRole(MyConstants.RoleDoctor))
            {
                Doctor doctor = db.Doctors.Include(d => d.CurDepartment).FirstOrDefault(y => y.UserId == userGuid);
                return View("Doctor", doctor);
            }
            else if (User.IsInRole(MyConstants.RolePatient))
            {
                Patient patient = db.Patients.FirstOrDefault(y => y.UserId == userGuid);
                return View("Patient", patient);
            }
            else if (User.IsInRole(MyConstants.RoleAdmin))
            {
                Admin admin = db.Admins.FirstOrDefault(y => y.UserId == userGuid);
                ViewBag.PatientCount = db.Patients.Count();
                ViewBag.DoctorCount = db.Doctors.Count();
                ViewBag.AppCount = db.Appointments.Count();
                ViewBag.BillCount = db.Bills.Count();
                ViewBag.DeptCount = db.Departments.Count();

                return View("Admin",admin);
            }
            else if (User.IsInRole(MyConstants.RoleAccountant))
            {
                Admin admin = db.Admins.FirstOrDefault(y => y.UserId == userGuid);
                ViewBag.PatientCount = db.Patients.Count();
                ViewBag.DoctorCount = db.Doctors.Count();
                ViewBag.AppCount = db.Appointments.Count();
                ViewBag.BillCount = db.Bills.Count();
                return View("Accountant",admin);
            }
            else return RedirectToAction("Login", "Account");
        }

        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Management");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register([Bind(Include = "Id,Name,Surname,DOB,Gender,Blood_Group,Email,Address,City,Phone")] Patient patient)
        {
            // basic password checks you already had
            if (Request["password"] != "")
            {
                if (Request["password"] != Request["passwordconfirm"])
                {
                    ViewBag.PassMess = "Password are different";
                    return View(patient);
                }
            }

            if (!ModelState.IsValid) return View(patient);

            // Use using blocks to ensure disposal of contexts
            using (var db = new HospitalSystem3Context())
            using (var userdb = new ApplicationDbContext())
            {
                var userStore = new UserStore<ApplicationUser>(userdb);
                var userManager = new ApplicationUserManager(userStore);

                var roleStore = new RoleStore<IdentityRole>(userdb);
                var roleManager = new RoleManager<IdentityRole>(roleStore);

                // prevent duplicate username/email
                if (userdb.Users.Any(x => x.UserName == patient.Email))
                {
                    ViewBag.SameEmail = "The email already exists. Take another";
                    return View(patient);
                }

                var newUser = new ApplicationUser
                {
                    Email = patient.Email,
                    UserName = patient.Email
                };

                // Create the user and check result
                var password = Request["password"]?.ToString() ?? "";
                var createResult = userManager.Create(newUser, password);
                if (!createResult.Succeeded)
                {
                    // Show the underlying Identity errors (password rules, duplicate, etc.)
                    ModelState.AddModelError("", "Could not create user: " + string.Join(" | ", createResult.Errors));
                    return View(patient);
                }

                // Ensure the role exists before assigning it
                var roleName = MyConstants.RolePatient;
                if (!roleManager.RoleExists(roleName))
                {
                    var roleCreate = roleManager.Create(new IdentityRole(roleName));
                    if (!roleCreate.Succeeded)
                    {
                        ModelState.AddModelError("", "Could not create role: " + string.Join(" | ", roleCreate.Errors));
                        return View(patient);
                    }
                }

                // Re-fetch the created user (safer)
                var createdUser = userManager.FindByName(newUser.UserName);
                if (createdUser == null)
                {
                    ModelState.AddModelError("", "User created but could not be found afterwards.");
                    return View(patient);
                }

                // Assign role and check result
                var addRoleResult = userManager.AddToRole(createdUser.Id, roleName);
                if (!addRoleResult.Succeeded)
                {
                    ModelState.AddModelError("", "Could not add role: " + string.Join(" | ", addRoleResult.Errors));
                    return View(patient);
                }

                // Link patient record to real user id and save
                patient.UserId = createdUser.Id;
                db.Patients.Add(patient);
                db.SaveChanges();

                // done - redirect to login or wherever appropriate
                return RedirectToAction("Login", "Account");
            }
        }

            
        }

    }
