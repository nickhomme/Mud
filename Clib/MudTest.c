#include "include/mud.h"
//
// Created by Nicholas Homme on 6/19/22.
//
int main(int argc, char **argv) {
  JavaVMOption* options = _java_jvm_options_va(1, "-Djava.class.path=/Users/nicholas/Documents/ct400/java/jt400.jar:/Users/nicholas/Documents/ct400/java");
  Java_JVM_Instance jvm = _java_jvm_create_instance(options, 1);

  jclass intCls = mud_get_class(jvm.env, "java/lang/Integer");
  jmethodID valueOfMid = mud_get_static_method(jvm.env, intCls, "valueOf", "(I)Ljava/lang/Integer;");
  jvalue intArg = {
      .i = 10
  };
  jobject intObj = mud_new_object(jvm.env, intCls, "(I)V", &intArg);
  jobject int2Obj = mud_call_static_method(jvm.env, intCls, valueOfMid, &intArg, Java_Object).value.l;
  jmethodID intToValueMid = mud_get_method(jvm.env, intCls, "intValue", "()I");
    jvalue args = {
        .i = 9
    };
  struct JavaCallResp_S resp = mud_call_method(jvm.env, intObj, intToValueMid, &args, Java_Int);
  struct JavaCallResp_S resp2 = mud_call_method(jvm.env, int2Obj, intToValueMid, &args, Java_Int);
  printf("Int: {%i} {%i}\n", resp.value.i, resp2.value.i);

//  jobject obj = _java_build_class_object(jvm.env, "MyTest", null);
//  Java_Args* args3 = _java_args_new_ptr(1);
//  Java_Args args = _java_args_new(1);
////  _java_args_add(&args, _java_arg_new_string("Howdy123"));
////  _java_call_method(jvm.env, obj, "echo", _java_type(Java_Void) , args);
////  _java_call_method(jvm.env, obj, "ping", _java_type_object(Java_Object_String), args);
////
//
//
//  _java_args_add(&args, _java_arg_new_float(12));
//  _java_call_method(jvm.env, obj, "Num", _java_type(Java_Int), &args);
//  _java_jvm_destroy_instance(jvm.jvm);
}
