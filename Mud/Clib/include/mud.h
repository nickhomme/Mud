#ifndef Mud_LIBRARY_H
#define Mud_LIBRARY_H

#include <jni.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

#include "java-arg.h"
#include "../src/memory-util.h"
void hello();

void _java_release_object(JNIEnv* env, jobject obj);

void interop_free(ptr pointer);

static const char* _java_jstring_to_string(JNIEnv* env, jstring message) {
  return (*env)->GetStringUTFChars(env, message, 0);
}

void _java_string_release(JNIEnv* env, jstring message, const char* msgChars);

Java_Args _java_args_new(int argAmnt);

Java_Args* _java_args_new_ptr(int argAmnt);

void _java_args_add(Java_Args *args, Java_Typed_Val arg);
//static JNIEXPORT void Java_Natives_printf(JNIEnv *env, jobject obj, jstring message) {
//  std::string msg = _java_jstring_to_string(message);
//  printf("%s\n", msg);
//}
typedef struct _Java_JVM_Instance {
  JavaVM* jvm;
  JNIEnv* env;
} Java_JVM_Instance;

Java_JVM_Instance _java_jvm_create_instance(const char*);
void _java_jvm_destroy_instance(JavaVM* jvm);

jclass _java_get_class(JNIEnv* env, const char* className);
jclass _java_get_obj_class(JNIEnv* env, jobject obj);

Java_Typed_Val _java_call_method_varargs(JNIEnv* env,
                                          jobject obj,
                                                const char* methodName,
                                          Java_Full_Type returnType,
                                          int argAmnt,
                                         Java_Typed_Val* args);
Java_Typed_Val _java_call_method(JNIEnv* env, jobject obj, const char* methodName, Java_Full_Type returnType, Java_Args* args);

void _java_call_method_void(JNIEnv* env, jobject obj, const char* methodName, Java_Full_Type returnType, Java_Args* args);
Java_Typed_Val _java_call_static_method_varargs(JNIEnv* env,
                                                       jclass cls,
                                                       const char* methodName,
                                                       Java_Full_Type returnType,
                                                       int argAmnt,
                                                Java_Typed_Val* args);
Java_Typed_Val _java_call_static_method(JNIEnv* env,
                                         jclass cls,
                                         const char* methodName,
                                         Java_Full_Type returnType,
                                         Java_Args* args);
Java_Typed_Val _java_call_static_method_named(JNIEnv* env,
                                               const char* className,
                                               const char* methodName,
                                               Java_Full_Type returnType,
                                               Java_Args* args);
Java_Typed_Val _java_call_static_method_named_varargs(JNIEnv* env,
                                                       const char* className,
                                                       const char* methodName,
                                                       Java_Full_Type returnType,
                                                       int argAmnt,
                                                      Java_Typed_Val* args);

jobject _java_build_object(JNIEnv* env, jclass cls);
jobject _java_build_class_object(JNIEnv* env, const char* className);

jfieldID _java_get_field_id(JNIEnv* env, const char* cls, const char* field, Java_Full_Type type);

jfieldID _java_get_field_id_by_class(JNIEnv* env, jclass cls, const char* field, Java_Full_Type type);

Java_Typed_Val _java_get_object_property(JNIEnv* env, jobject object, jfieldID field, Java_Full_Type type);


Java_Typed_Val _java_get_object_property_by_name(JNIEnv* env, jobject object, const char* field, Java_Full_Type type);

#endif //MUD_LIBRARY_H
