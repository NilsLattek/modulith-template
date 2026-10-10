using Riok.Mapperly.Abstractions;

// Every DTO property needs a source; entity members a DTO leaves out are fine. RMG012 is an
// error in .editorconfig, so a DTO property with nothing to map from fails the build.
[assembly: MapperDefaults(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
