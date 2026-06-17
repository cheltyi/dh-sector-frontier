cmd-persistenceload-desc = Loads a full map file from the given path into the world.
cmd-persistenceload-help = Usage: persistenceload <path>

cmd-persistenceloadchar-desc = Loads a single saved entity from the given path and teleports it to you.
cmd-persistenceloadchar-help = Usage: persistenceloadchar <path>

cmd-persistenceloadgrid-desc = Loads a grid file onto a map at an optional offset/rotation.
cmd-persistenceloadgrid-help = Usage: persistenceloadgrid <mapId> <path> [x] [y] [rotDeg] [storeUids]

cmd-persistencesavegrid-desc = Saves a grid entity to a file, dumping players off it and deleting it afterward.
cmd-persistencesavegrid-help = Usage: persistencesavegrid <netEntityId> <filePath>

cmd-persistencesavechar-desc = Saves a single entity (character) to a file.
cmd-persistencesavechar-help = Usage: persistencesavechar <netEntityId> [filePath - default: game.map (CCVar)]

cmd-persistenceload-result = Map load status: {$status} (entity: {$entity})
