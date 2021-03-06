﻿using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.AccessControl;
using CoursesAPI.Models;
using CoursesAPI.Services.DataAccess;
using CoursesAPI.Services.Exceptions;
using CoursesAPI.Services.Models.Entities;

namespace CoursesAPI.Services.Services
{
	public class CoursesServiceProvider
	{
		private readonly IUnitOfWork _uow;

		private readonly IRepository<CourseInstance> _courseInstances;
		private readonly IRepository<TeacherRegistration> _teacherRegistrations;
		private readonly IRepository<CourseTemplate> _courseTemplates; 
		private readonly IRepository<Person> _persons;

		public CoursesServiceProvider(IUnitOfWork uow)
		{
			_uow = uow;

			_courseInstances      = _uow.GetRepository<CourseInstance>();
			_courseTemplates      = _uow.GetRepository<CourseTemplate>();
			_teacherRegistrations = _uow.GetRepository<TeacherRegistration>();
			_persons              = _uow.GetRepository<Person>();
		}

		/// <summary>
		/// You should implement this function, such that all tests will pass.
		/// </summary>
		/// <param name="courseInstanceID">The ID of the course instance which the teacher will be registered to.</param>
		/// <param name="model">The data which indicates which person should be added as a teacher, and in what role.</param>
		/// <returns>Should return basic information about the person.</returns>
		public PersonDTO AddTeacherToCourse(int courseInstanceID, AddTeacherViewModel model)
		{
			// Check if courseID is correct
		    var courseInstance = (from c in _courseInstances.All()
		        where c.ID == courseInstanceID
		        select c).SingleOrDefault();

		    if (courseInstance == null){
		        throw new AppObjectNotFoundException();
		    }

            // Check if teacher SSN is in the system
		    var teacher = (from p in _persons.All()
		        where p.SSN == model.SSN
		        select p).SingleOrDefault();

		    if (teacher == null){
		        throw new AppObjectNotFoundException();
		    }

            // Get the list of teachers assigned for this course
            var teachersForCourse = (from tr in _teacherRegistrations.All()
                                     where tr.CourseInstanceID == courseInstanceID
                                     select tr).ToList();

		    // If the teacher being registered is a main teacher
            // and the course already has a main teacher registered;
            // throw an exception
            if (model.Type == TeacherType.MainTeacher && 
                teachersForCourse.Any(t => t.Type == TeacherType.MainTeacher))
            {
                throw new AppValidationException("COURSE_ALREADY_HAS_A_MAIN_TEACHER");
            }

            // If the teacher being registered is already registered as a
            // teacher in the course; throw an exception
		    if (teachersForCourse.Any(t => t.SSN == model.SSN))
		    {
		        throw new AppValidationException("PERSON_ALREADY_REGISTERED_TEACHER_IN_COURSE");
		    }

            // If all seems to be OK, register the teacher to the course
		    var teacherRegistration = new TeacherRegistration
		    {
                SSN = model.SSN,
                CourseInstanceID = courseInstanceID,
                Type = model.Type
		    };

            _teacherRegistrations.Add(teacherRegistration);
            _uow.Save();

            // Display the teacher that was added to the course
		    var personDTO = new PersonDTO
		    {
                Name = teacher.Name,
                SSN = teacher.SSN
		    };

			return personDTO;
		}

		/// <summary>
		/// Finds CourseInstances taught on the given semester.
		/// If no semester is given, the current semester "20153" is used instead.
		/// </summary>
		/// <param name="semester">The semester to get courses from</param>
		/// <returns>A List of CourseInstanceDTOs taught on the given semester</returns>
		public List<CourseInstanceDTO> GetCourseInstancesBySemester(string semester = null)
		{
            // Assign a default semester if no semester is given
			if (string.IsNullOrEmpty(semester))
			{
				semester = "20153";
			}

            // Construct the list of courses tought in the given semester
			var courses = (from c in _courseInstances.All()
				           join ct in _courseTemplates.All() on c.CourseID equals ct.CourseID
				           where c.SemesterID == semester
				           select new CourseInstanceDTO
				           {
					           Name               = ct.Name,
					           TemplateID         = ct.CourseID,
					           CourseInstanceID   = c.ID,
					           MainTeacher        = ""
				           }).ToList();

            // Find main teacher name
		    foreach (var ciDTO in courses)
		    {
		        var mainTeacherRegistration = (from tr in _teacherRegistrations.All()
		            where tr.CourseInstanceID == ciDTO.CourseInstanceID
		            where tr.Type == TeacherType.MainTeacher
		            select tr).SingleOrDefault();

		        if (mainTeacherRegistration != null)
		        {
		            var mainTeacher = (from p in _persons.All()
                                       where p.SSN == mainTeacherRegistration.SSN
                                       select p).SingleOrDefault();

                    if (mainTeacher != null)
                    {
                        ciDTO.MainTeacher = mainTeacher.Name;
                    }
		        } 
		    }

			return courses;
		}
	}
}
