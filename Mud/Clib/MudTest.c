#include "include/mud.h"
//
// Created by Nicholas Homme on 6/19/22.
//
int main(int argc, char **argv) {
  char* jvmArgs = "-Djava.class.path=/Users/nicholas/Documents/ct400/java/jt400.jar:/Users/nicholas/Documents/ct400/java";
  Java_JVM_Instance jvm = _java_jvm_create_instance(jvmArgs);

  jobject obj = _java_build_class_object(jvm.env, "MyTest");
  Java_Args* args3 = _java_args_new_ptr(1);
  Java_Args args = _java_args_new(1);
//  _java_args_add(&args, _java_arg_new_string("Howdy123"));
//  _java_call_method(jvm.env, obj, "echo", _java_type(Java_Void) , args);
//  _java_call_method(jvm.env, obj, "ping", _java_type_object(Java_Object_String), args);
//


  _java_args_add(&args, _java_arg_new_float(12));
  _java_call_method(jvm.env, obj, "Num", _java_type(Java_Int), &args);
  _java_jvm_destroy_instance(jvm.jvm);
}