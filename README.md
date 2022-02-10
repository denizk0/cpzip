# cpzip

Copies file to the target zip archive, with nested archives support.

    Usage: cpzip source_file target_file target_path [options]

    -n, --no-overwrite      Do not overwrite existing files.
    -v, --verbose           Set output to verbose messages.
    --help                  Display this help screen.
    --version               Display version information.
    source_file (pos. 0)    Required. Source file to copy.
    target_file (pos. 1)    Required. Target file to copy to.
    target_path (pos. 2)    Required. Path within the target file. Use '/' as a separator or as a root path.

Example:

    cpzip my_photo.png my_photos.zip christmas/this_year.zip/new

will copy `my_photo.png` to the folder `new` of the nested file `this_year.zip` updating `my_photos.zip` accordingly.

May be useful for developers for quick updating libs in the uber-jar file:

    cpzip ~/.m2/repository/joda-time/joda-time/2.9.9/joda-time-2.9.9.jar camunda-bpm-run-7.16.0.zip internal/camunda-bpm-run-core.jar/BOOT-INF/lib -v