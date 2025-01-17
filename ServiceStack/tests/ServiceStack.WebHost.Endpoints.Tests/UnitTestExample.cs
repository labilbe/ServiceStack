﻿// Copyright (c) ServiceStack, Inc. All Rights Reserved.
// License: https://raw.github.com/ServiceStack/ServiceStack/master/license.txt


using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Funq;
using NUnit.Framework;
using ServiceStack.Configuration;
using ServiceStack.Data;
using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.Testing;
using ServiceStack.Text;
using ServiceStack.Web;

namespace ServiceStack.WebHost.Endpoints.Tests
{
    // Request DTOs
    public class FindRockstars
    {
        public int? Aged { get; set; }
        public bool? Alive { get; set; }
    }

    public class GetStatus
    {
        public string LastName { get; set; }
    }

    public enum LivingStatus
    {
        Alive,
        Dead
    }

    // Types
    public class Rockstar
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int? Age { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime? DateDied { get; set; }
        public LivingStatus LivingStatus { get; set; }

        protected bool Equals(Rockstar other)
        {
            return Id == other.Id && 
                   FirstName == other.FirstName && 
                   LastName == other.LastName && 
                   Age == other.Age && 
                   DateOfBirth.Equals(other.DateOfBirth) && 
                   Nullable.Equals(DateDied, other.DateDied) && 
                   LivingStatus == other.LivingStatus;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Rockstar)obj);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, FirstName, LastName, Age, DateOfBirth, DateDied, (int)LivingStatus);
        }
    }

    public class RockstarStatus
    {
        public int Age { get; set; }
        public bool Alive { get; set; }
    }

    public class PagingTest
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
    }

    // Implementation
    public class SimpleService : Service
    {
        public IRockstarRepository RockstarRepository { get; set; }

        public List<Rockstar> Get(FindRockstars request)
        {
            return request.Aged.HasValue
                ? Db.Select<Rockstar>(q => q.Age == request.Aged.Value)
                : Db.Select<Rockstar>();
        }

        public RockstarStatus Get(GetStatus request)
        {
            var rockstar = RockstarRepository.GetByLastName(request.LastName);
            if (rockstar == null)
                throw HttpError.NotFound("'{0}' is not a Rockstar".Fmt(request.LastName));

            var status = new RockstarStatus
            {
                Alive = RockstarRepository.IsAlive(request.LastName)
            }.PopulateWith(rockstar); //Populates with matching fields

            return status;
        }
    }

    //Custom Repository
    public interface IRockstarRepository
    {
        Rockstar GetByLastName(string lastName);
        bool IsAlive(string lastName);
    }

    public class RockstarRepository : RepositoryBase, IRockstarRepository
    {
        public Rockstar GetByLastName(string lastName)
        {
            return Db.Single<Rockstar>(q => q.LastName == lastName);
        }

        readonly HashSet<string> fallenLegends = new HashSet<string> {
            "Hendrix", "Hendrix", "Cobain", "Presley", "Jackson"
        };

        public bool IsAlive(string lastName)
        {
            return !fallenLegends.Contains(lastName);
        }
    }

    //Use base class to keep common boilerplate
    public class RepositoryBase : IDisposable
    {
        public IDbConnectionFactory DbFactory { get; set; }

        IDbConnection db;
        protected IDbConnection Db
        {
            get { return db ?? (db = DbFactory.Open()); }
        }

        public void Dispose()
        {
            if (db != null)
                db.Dispose();
        }
    }

    [TestFixture]
    public class UnitTestExample
    {
        public static List<Rockstar> SeedData = new[] {
            new Rockstar { Id = 1, FirstName = "Jimi", LastName = "Hendrix", Age = 27, DateOfBirth = new DateTime(1942, 11, 27), DateDied = new DateTime(1970, 09, 18), },
            new Rockstar { Id = 2, FirstName = "Jim", LastName = "Morrison", Age = 27, DateOfBirth = new DateTime(1943, 12, 08), DateDied = new DateTime(1971, 07, 03),  },
            new Rockstar { Id = 3, FirstName = "Kurt", LastName = "Cobain", Age = 27, DateOfBirth = new DateTime(1967, 02, 20), DateDied = new DateTime(1994, 04, 05), },
            new Rockstar { Id = 4, FirstName = "Elvis", LastName = "Presley", Age = 42, DateOfBirth = new DateTime(1935, 01, 08), DateDied = new DateTime(1977, 08, 16), },
            new Rockstar { Id = 5, FirstName = "David", LastName = "Grohl", Age = 44, DateOfBirth = new DateTime(1969, 01, 14), },
            new Rockstar { Id = 6, FirstName = "Eddie", LastName = "Vedder", Age = 48, DateOfBirth = new DateTime(1964, 12, 23), },
            new Rockstar { Id = 7, FirstName = "Michael", LastName = "Jackson", Age = 50, DateOfBirth = new DateTime(1958, 08, 29), DateDied = new DateTime(2009, 06, 05), },
        }.ToList();

        private ServiceStackHost appHost;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            appHost = new BasicAppHost().Init();
            var container = appHost.Container;

            container.Register<IDbConnectionFactory>(
                new OrmLiteConnectionFactory(":memory:", SqliteDialect.Provider));

            container.RegisterAutoWiredAs<RockstarRepository, IRockstarRepository>();

            container.RegisterAutoWired<SimpleService>();

            using (var db = container.Resolve<IDbConnectionFactory>().Open())
            {
                db.DropAndCreateTable<Rockstar>();
                db.InsertAll(SeedData);
            }
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            appHost.Dispose();
        }

        [Test]
        public void Using_in_memory_database()
        {
            var service = appHost.Container.Resolve<SimpleService>();  //Resolve auto-wired service

            var rockstars = service.Get(new FindRockstars { Aged = 27 });

            Assert.That(rockstars.Count, Is.EqualTo(SeedData.Count(x => x.Age == 27)));

            var status = service.Get(new GetStatus { LastName = "Vedder" });
            Assert.That(status.Age, Is.EqualTo(48));
            Assert.That(status.Alive, Is.True);

            status = service.Get(new GetStatus { LastName = "Hendrix" });
            Assert.That(status.Age, Is.EqualTo(27));
            Assert.That(status.Alive, Is.False);

            Assert.Throws<HttpError>(() =>
                service.Get(new GetStatus { LastName = "Unknown" }));
        }

        public class RockstarRepositoryMock : IRockstarRepository
        {
            public Rockstar GetByLastName(string lastName)
            {
                return lastName == "Vedder"
                    ? new Rockstar { Id = 6, FirstName = "Eddie", LastName = "Vedder", Age = 48 }
                    : null;
            }

            public bool IsAlive(string lastName)
            {
                return lastName == "Grohl" || lastName == "Vedder";
            }
        }

        [Test]
        public void Using_manual_dependency_injection()
        {
            var service = new SimpleService
            {
                RockstarRepository = new RockstarRepositoryMock()
            };

            var status = service.Get(new GetStatus { LastName = "Vedder" });
            Assert.That(status.Age, Is.EqualTo(48));
            Assert.That(status.Alive, Is.True);

            Assert.Throws<HttpError>(() =>
                service.Get(new GetStatus { LastName = "Hendrix" }));
        }
    }
}