using System;
using System.Collections.Generic;
using N2.Definitions;

namespace N2.Integrity
{
	/// <summary>
	/// Class decoration that lets N2 which zones a data item can be added to. 
	/// This is mostly a hint for the user interface. Placing an item in a zone
    /// merly means assigning the child item's ZoneName property a meaningful 
    /// string.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class)]
	public class AllowedZonesAttribute : Attribute, IInheritableDefinitionRefiner
	{
		/// <summary>Initializes a new instance of the AllowedZonesAttribute which is used to restrict which zones item may have.</summary>
		public AllowedZonesAttribute(AllowedZones allowedZones)
		{
			if(allowedZones == AllowedZones.All)
				zoneNames = null;
			else 
				zoneNames = new string[0];
		}

		/// <summary>Initializes a new instance of the AllowedZonesAttribute which is used to restrict which zones item may have.</summary>
		/// <param name="zoneNames">A list of allowed zone names. Null is interpreted as any/all zone.</param>
		public AllowedZonesAttribute(params string[] zoneNames)
		{
			this.zoneNames = zoneNames;
		}

		private string[] zoneNames;

		/// <summary>Gets or sets zones the item can be added to.</summary>
		public string[] ZoneNames
		{
			get { return zoneNames; }
			set { zoneNames = value; }
		}

		public void Refine(ItemDefinition currentDefinition, IList<ItemDefinition> allDefinitions)
		{
			currentDefinition.AllowedZoneNames = ZoneNames;
		}
	}
}