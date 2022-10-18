cmake_minimum_required(VERSION 3.22)
project(Mud)
set (CMAKE_SHARED_LIBRARY_PREFIX "lib")

# add these lines
include(GenerateExportHeader)

find_program(CCACHE_PROGRAM ccache)
if(CCACHE_PROGRAM)
    set_property(GLOBAL PROPERTY RULE_LAUNCH_COMPILE "${CCACHE_PROGRAM}")
endif()
#set(CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH} "${CMAKE_SOURCE_DIR}/Modules/")
set(CMAKE_C_STANDARD 17)

add_library(Mud SHARED include/mud.h src/mud.c include/java-arg.h src/memory-util.h src/memory-util.c src/signature_parse.c src/signature_parse.h)

find_package(JNI REQUIRED)
find_package(Java REQUIRED)
include_directories(${JNI_INCLUDE_DIRS})

set_target_properties( Mud
                       PROPERTIES
                       ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Native"
                       LIBRARY_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Native"
                       RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/../Native"
                       )

if (DEFINED wasm)
elseif (WIN32)
    generate_export_header(Mud)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
    set_target_properties(Mud PROPERTIES PREFIX "../")
    set_target_properties(Mud PROPERTIES IMPORT_PREFIX "../")
elseif(APPLE)
    include_directories(${JNI_INCLUDE_DIRS}/darwin)
elseif(UNIX)
endif()

target_link_libraries(Mud ${JNI_LIBRARIES})

message(${JNI_INCLUDE_DIRS})
#message(${JAVA_INCLUDE_DIRS})
message("====")
message(${JNI_LIBRARIES})
#message(${JAVA_LIBRARIES})
#target_include_directories(Mud PUBLIC ${JNI_INCLUDE_DIRS})

add_executable(MudTest MudTest.c)
target_link_libraries(MudTest Mud)
#target_include_directories(MudTest PUBLIC ${JNI_INCLUDE_DIRS})
