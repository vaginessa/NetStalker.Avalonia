﻿using AutoMapper;
using NetStalkerAvalonia.Services;
using NetStalkerAvalonia.Services.Implementations.RulesService;
using System.IO.Abstractions;

namespace NetStalker.Tests.AutoData.Customizations
{
	public class RuleServiceCustomization : ICustomization
	{
		public void Customize(IFixture fixture)
		{
			fixture.Customizations.Add(new TypeRelay(typeof(IRuleService), typeof(RuleService)));
			fixture.Register(() => new RuleService(fixture.Create<IMapper>(), fixture.Create<IFileSystem>()));
		}
	}
}