﻿// Copyright (c) 2014 Eberhard Beilharz
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;

namespace BuildDependency.TeamCity.RestClasses
{
	public class Project
	{
		//<project id="project43" name="Adapt It" href="/guestAuth/app/rest/7.0/projects/id:project43"/>
		public string Name { get; set; }
		public string Id { get; set; }
		public string Href { get; set; }

		public override bool Equals(object obj)
		{
			var otherProj = obj as Project;
			if (otherProj != null)
				return Id == otherProj.Id;
			return base.Equals(obj);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode() ^ Id.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("[Project: Name={0}, Id={1}, Href={2}]", Name, Id, Href);
		}

		public string Text { get { return Name; }}
	}
}

