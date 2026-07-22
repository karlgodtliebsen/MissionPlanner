# Vendored MAVLink dialect sources

These XML files are copied without modification from the official
[`mavlink/mavlink`](https://github.com/mavlink/mavlink) repository at revision
`de1e078a3a7c53c9262a95b7417959a0f8bf4150`.

The selected root is `ardupilotmega.xml`. Its transitive include closure contains:

`common.xml`, `standard.xml`, `minimal.xml`, `uAvionix.xml`, `icarous.xml`,
`loweheiser.xml`, `cubepilot.xml`, and `csAirLink.xml`.

`COPYING` contains the upstream license. Normal builds and tests use these vendored inputs
and do not access the network. Do not edit the XML files manually.
