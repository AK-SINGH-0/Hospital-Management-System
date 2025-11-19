using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using HospitalSystem.Data;
using HospitalSystem.Models;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json;

namespace HospitalSystem.Controllers
{
    /*doktor/hasta silindiğinde randevular ne olmalı */
    public class AppointmentsController : Controller
    {
        private HospitalSystem3Context db = new HospitalSystem3Context();

        // GET: Appointments
        [Authorize(Roles = MyConstants.RolePatient + "," + MyConstants.RoleDoctor)]
        public ActionResult Index()
        {
            var appointments = db.Appointments.Include(a => a.Doctor).Include(a => a.Patient);

            if (User.IsInRole(MyConstants.RolePatient))
            {
                string patientGuid = User.Identity.GetUserId();
                var patient = db.Patients.FirstOrDefault(y => y.UserId == patientGuid);
                if (patient != null)
                {
                    int patientId = patient.Id;
                    appointments = db.Appointments
                        .Where(x => x.Patient_ID == patientId)
                        .Include(a => a.Doctor)
                        .Include(a => a.Patient);
                }
                else
                {
                    // no patient found: show empty list
                    appointments = Enumerable.Empty<Appointment>().AsQueryable();
                }
            }
            else if (User.IsInRole(MyConstants.RoleDoctor))
            {
                string doctorGuid = User.Identity.GetUserId();
                var doctor = db.Doctors.FirstOrDefault(y => y.UserId == doctorGuid);
                if (doctor != null)
                {
                    int doctorId = doctor.ID;
                    appointments = db.Appointments
                        .Where(x => x.Doctor_ID == doctorId)
                        .Include(a => a.Doctor)
                        .Include(a => a.Patient);
                }
                else
                {
                    appointments = Enumerable.Empty<Appointment>().AsQueryable();
                }
            }

            return View(appointments.ToList());
        }

        [Authorize(Roles = MyConstants.RolePatient + "," + MyConstants.RoleDoctor)]
        // GET: Appointments/Details/5
        public string Details(int? id) // RANDEVU DETAYLARI JSON İLE FETCH GET OLARAK GÖSTERİLECEK
        {
            if (id == null)
            {
                return "403";
            }

            Appointment appointment = db.Appointments.Find(id);
            if (appointment == null)
            {
                return "404";
            }

            string userGuid = User.Identity.GetUserId();

            if (User.IsInRole(MyConstants.RolePatient))
            {
                var patient = db.Patients.FirstOrDefault(y => y.UserId == userGuid);
                if (patient == null || patient.Id != appointment.Patient_ID)
                {
                    return "Forbidden Access";
                }
            }
            else if (User.IsInRole(MyConstants.RoleDoctor))
            {
                var doctor = db.Doctors.FirstOrDefault(y => y.UserId == userGuid);
                if (doctor == null || doctor.ID != appointment.Doctor_ID)
                {
                    return "Forbidden Access";
                }
            }

            // avoid including full related entities in JSON
            appointment.Patient = null;
            appointment.Doctor = null;

            return JsonConvert.SerializeObject(appointment, new JsonSerializerSettings() { DateFormatString = "yyyy-MM-ddThh:mm:ssZ" });
        }

        // GET: Appointments/Create
        [Authorize(Roles = MyConstants.RolePatient)]
        public ActionResult Create()
        {
            // Prepare doctor select list with department names safely
            var doctors = db.Doctors.ToList();
            var departments = db.Departments.ToList(); // load once

            var doctorItems = doctors.Select(d =>
            {
                var dept = departments.FirstOrDefault(x => x.ID == d.CurDeptartmentID);
                string deptName = dept != null ? dept.Name : "No Department";
                return new
                {
                    ID = d.ID,
                    Name = d.Name + " - " + deptName
                };
            }).ToList();

            ViewBag.Doctor_ID = new SelectList(doctorItems, "ID", "Name");
            ViewBag.Patient_ID = new SelectList(db.Patients, "Id", "Name");
            return View();
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = MyConstants.RolePatient)]
        public ActionResult Create([Bind(Include = "Id,Description,AppointmentDate,Doctor_ID,Patient_ID")] Appointment appointment)
        {
            string patientGuid = User.Identity.GetUserId();
            var patientEntity = db.Patients.FirstOrDefault(y => y.UserId == patientGuid);
            if (patientEntity == null)
            {
                // no patient found for current user - forbidden or error
                ModelState.AddModelError("", "Current user is not linked to a patient record.");
                // re-populate dropdowns and return view
                PopulateDoctorPatientSelectLists(appointment);
                return View(appointment);
            }

            appointment.Patient_ID = patientEntity.Id;

            if (ModelState.IsValid)
            {
                db.Appointments.Add(appointment);
                db.SaveChanges();

                // create the bill safely
                var doctor = db.Doctors.FirstOrDefault(x => x.ID == appointment.Doctor_ID);
                if (doctor != null)
                {
                    var dept = db.Departments.FirstOrDefault(x => x.ID == doctor.CurDeptartmentID);
                    decimal amount = (dept != null) ? dept.PriceUnit : 0m;
                    Bill bill = new Bill()
                    {
                        Appointment_ID = appointment.Id,
                        IsPaid = false,
                        Amount = amount,
                        Issued_Date = DateTime.Now
                    };
                    db.Bills.Add(bill);
                    db.SaveChanges();
                }

                return RedirectToAction("Index");
            }

            // on failure re-populate dropdowns and show model errors
            PopulateDoctorPatientSelectLists(appointment);
            return View(appointment);
        }

        [Authorize(Roles = MyConstants.RolePatient + "," + MyConstants.RoleDoctor)]
        // GET: Appointments/Edit/5
        public ActionResult Edit(int? id)
        {
            string userGuid = User.Identity.GetUserId();
            int userId = 0;

            if (User.IsInRole(MyConstants.RolePatient))
            {
                var patient = db.Patients.FirstOrDefault(y => y.UserId == userGuid);
                if (patient != null) userId = patient.Id;
            }
            else if (User.IsInRole(MyConstants.RoleDoctor))
            {
                var doctor = db.Doctors.FirstOrDefault(y => y.UserId == userGuid);
                if (doctor != null) userId = doctor.ID;
            }

            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Appointment appointment = db.Appointments.Find(id);
            if (appointment == null)
                return HttpNotFound();

            if (User.IsInRole(MyConstants.RolePatient) && userId != appointment.Patient_ID)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
            else if (User.IsInRole(MyConstants.RoleDoctor) && userId != appointment.Doctor_ID)
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

            var doctors = db.Doctors.ToList();
            var departments = db.Departments.ToList();

            var doctorItems = doctors.Select(d =>
            {
                var dept = departments.FirstOrDefault(x => x.ID == d.CurDeptartmentID);
                string deptName = dept != null ? dept.Name : "No Department";
                return new
                {
                    ID = d.ID,
                    Name = d.Name + " - " + deptName
                };
            }).ToList();

            ViewBag.Doctor_ID = new SelectList(doctorItems, "ID", "Name", appointment.Doctor_ID);
            ViewBag.Patient_ID = new SelectList(db.Patients, "Id", "Name", appointment.Patient_ID);

            var p = db.Patients.FirstOrDefault(x => appointment.Patient_ID == x.Id);
            ViewBag.Patient_Name = p != null ? (p.Name + " " + p.Surname) : "Unknown";

            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = MyConstants.RolePatient + "," + MyConstants.RoleDoctor)]
        public ActionResult Edit([Bind(Include = "Id,Description,Consultant_Fee,AppointmentDate,Doctor_ID,Patient_ID")] Appointment appointment)
        {
            if (ModelState.IsValid)
            {
                db.Entry(appointment).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            // previously you redirected to Index here which hides validation errors.
            // Re-populate dropdowns and return view to show errors.
            PopulateDoctorPatientSelectLists(appointment);
            return View(appointment);
        }

        // POST: Appointments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = MyConstants.RolePatient + "," + MyConstants.RoleDoctor)]
        public ActionResult DeleteConfirmed(int id)
        {
            Appointment appointment = db.Appointments.Find(id);
            if (appointment == null)
            {
                return HttpNotFound();
            }

            db.Appointments.Remove(appointment);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // small helper to set ViewBag lists
        private void PopulateDoctorPatientSelectLists(Appointment appointment)
        {
            var doctors = db.Doctors.ToList();
            var departments = db.Departments.ToList();

            var doctorItems = doctors.Select(d =>
            {
                var dept = departments.FirstOrDefault(x => x.ID == d.CurDeptartmentID);
                string deptName = dept != null ? dept.Name : "No Department";
                return new
                {
                    ID = d.ID,
                    Name = d.Name + " - " + deptName
                };
            }).ToList();

            ViewBag.Doctor_ID = new SelectList(doctorItems, "ID", "Name", appointment?.Doctor_ID);
            ViewBag.Patient_ID = new SelectList(db.Patients, "Id", "Name", appointment?.Patient_ID);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
