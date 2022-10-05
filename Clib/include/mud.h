#ifndef Mud_LIBRARY_H
#define Mud_LIBRARY_H

#include <jni.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

#include "java-arg.h"
#include "../src/memory-util.h"
void hello();



struct JavaCallResp_S {
  bool is_void;
  bool is_exception;
  jvalue value;
};

static jthrowable _java_jvm_check_exception(JNIEnv* env) {
  if (!(*env)->ExceptionCheck(env)) {
    puts("Is NOT Exception");
    return null;
  }
  puts("Is DEF Exception");
  printf("[Exception]:\n");
  (*env)->ExceptionDescribe(env);
  jthrowable ex = (*env)->ExceptionOccurred(env);
  (*env)->ExceptionClear(env);
  return ex;
}

void _java_release_object(JNIEnv* env, jobject obj);

void interop_free(ptr pointer);

static const char* _java_jstring_to_string(JNIEnv* env, jstring message) {
  return (*env)->GetStringUTFChars(env, message, 0);
}

void _java_string_release(JNIEnv* env, jstring message, const char* msgChars);

//static JNIEXPORT void Java_Natives_printf(JNIEnv *env, jobject obj, jstring message) {
//  std::string msg = _java_jstring_to_string(message);
//  printf("%s\n", msg);
//}
typedef struct _Java_JVM_Instance {
  JavaVM* jvm;
  JNIEnv* env;
} Java_JVM_Instance;

JavaVMOption* _java_jvm_options(size_t amnt);

JavaVMOption* _java_jvm_options_va(size_t amnt, ...);
JavaVMOption* _java_jvm_options_str_arr(size_t amnt, const char** options);
Java_JVM_Instance _java_jvm_create_instance(JavaVMOption* options, int amnt);
void _java_jvm_destroy_instance(JavaVM* jvm);



struct Java_String_Resp {
  jobject java_ptr;
  const char* char_ptr;
};

struct Java_String_Resp mud_string_new(JNIEnv *env, const char* msg);

//Java_Typed_Val _java_call_method_manual(JNIEnv* env,
//                                 jobject obj,
//                                 const char* methodName,
//                                 const char* retType,
//                                 const char* argsType);


struct Java_Exception_S {
  char* msg;
  char* stack_trace;
};

char* mud_get_exception_msg(JNIEnv* env, jthrowable ex, jmethodID getCauseMethod,
                            jmethodID  getStackMethod, jmethodID exToStringMethod,
                            jmethodID frameToStringMethod, bool isTop);

void _java_add_class_path(JNIEnv* env, const char* path);
jclass mud_get_class(JNIEnv* env, const char* className);
jclass mud_get_class_of_obj(JNIEnv* env, jobject obj);

jobject mud_new_object(JNIEnv* env, jclass cls, const char* signature, const jvalue * args);

jmethodID mud_get_static_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);
jmethodID mud_get_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);



static jvalue map_value(Java_Type type, ptr val) {
  jvalue value = {};
  if (type == Java_Bool) {
    value.b = *((jbyte*)val);
  } else if (type == Java_Int) {
    value.i = *((jint*)val);
  } else if (type == Java_Long) {
    value.j = *((jlong *)val);
  } else if (type == Java_Byte) {
    value.b = *((jbyte *)val);
  } else if (type == Java_Char) {
    value.c = *((jchar *)val);
  } else if (type == Java_Short) {
    value.s = *((jshort *)val);
  } else if (type == Java_Float) {
    value.f = *((jfloat *)val);
  } else if (type == Java_Double) {
    value.d = *((jdouble*)val);
  } else if (type == Java_Object) {
    value.l = *((jobject*)val);
  }
  return value;
}
static struct JavaCallResp_S map_call_result(JNIEnv* env, Java_Type type, ptr val) {
  struct JavaCallResp_S result = {
    .is_exception = false,
    .is_void = false,
    .value = map_value(type, val),
  };
  if (type == Java_Void) {
    result.is_void = true;
  }
  jthrowable exception = _java_jvm_check_exception(env);
  printf("Excption???? [%i]\n", !!exception);
  if (exception) {
    result.value.l = exception;
    result.is_exception = true;
  }
  return result;
}

static struct JavaCallResp_S mud_call_handler(JNIEnv* env, ptr objOrCls, jmethodID method, const jvalue* args, Java_Type type, bool isStatic) {
#define call(name) (isStatic ? (*env)->CallStatic##name##MethodA : (*env)->Call##name##MethodA)(env, objOrCls, method, args)
#define retCallMap(name, jtype) jtype val = call(name); return map_call_result(env, type, &val);
  struct JavaCallResp_S result;
  printf("Calling %s Method: %p with return of %i on %p\n", isStatic ? "static" : "member", method, type, objOrCls);

  if (type == Java_Bool) {
    retCallMap(Boolean, jboolean)
  } else if (type == Java_Int) {
    retCallMap(Int, jint)
  } else if (type == Java_Long) {
    retCallMap(Long, jlong)
  } else if (type == Java_Byte) {
    retCallMap(Byte, jbyte)
  } else if (type == Java_Char) {
    retCallMap(Char, jchar)
  } else if (type == Java_Short) {
    retCallMap(Short, jshort)
  } else if (type == Java_Float) {
    retCallMap(Float, jfloat)
  } else if (type == Java_Double) {
    retCallMap(Double, jdouble)
  } else if (type == Java_Void) {
    call(Void);
    return map_call_result(env, type, null);
  } else {
    retCallMap(Object, jobject)
  }

}

struct JavaCallResp_S mud_call_static_method(JNIEnv* env, jobject obj, jmethodID method, const jvalue* args, Java_Type type);

struct JavaCallResp_S mud_call_method(JNIEnv* env, jobject obj, jmethodID method, const jvalue* args, Java_Type type);

static struct JavaCallResp_S mud_call_method_by_name(JNIEnv* env, jclass cls, const char* methodName, const char* signature, const jvalue* args, Java_Type type) {

  return mud_call_method(env, cls, mud_get_method(env, cls, methodName, signature), args, type);
}

static struct JavaCallResp_S mud_call_static_method_by_name(JNIEnv* env, jclass cls, const char* methodName, const char* signature, const jvalue* args, Java_Type type) {

  return mud_call_static_method(env, cls, mud_get_static_method(env, cls, methodName, signature), args, type);
}


char* mud_jstring_to_string(JNIEnv* env, jstring jstr);

jfieldID mud_get_field_id(JNIEnv* env, jclass cls, const char* field, const char* signature);

static jvalue mud_field_handler(JNIEnv* env, ptr objOrCls, jfieldID field, Java_Type type, bool isStatic) {
#define get(name) (isStatic ? (*env)->GetStatic##name##Field : (*env)->Get##name##Field)(env, objOrCls, field)
#define retGetMap(name, jtype) return (jvalue) get(name);
  struct JavaCallResp_S result;

  if (type == Java_Bool) {
    retGetMap(Boolean, jboolean)
  } else if (type == Java_Int) {
    retGetMap(Int, jint)
  } else if (type == Java_Long) {
    retGetMap(Long, jlong)
  } else if (type == Java_Byte) {
    retGetMap(Byte, jbyte)
  } else if (type == Java_Char) {
    retGetMap(Char, jchar)
  } else if (type == Java_Short) {
    retGetMap(Short, jshort)
  } else if (type == Java_Float) {
    retGetMap(Float, jfloat)
  } else if (type == Java_Double) {
    retGetMap(Double, jdouble)
  } else {
    retGetMap(Object, jobject)
  }

}


jvalue mud_get_field_value(JNIEnv* env, jobject cls, jfieldID field, Java_Type type);
jvalue mud_get_static_field_value(JNIEnv* env, jclass cls, jfieldID field, Java_Type type);
bool mud_instance_of(JNIEnv* env, jobject obj, jclass cls);

size_t mud_array_length(JNIEnv* env, jarray arr);
jvalue mud_array_get_at(JNIEnv* env, jarray arr, int index, Java_Type type);
//jvalue* mud_array_get_all(JNIEnv* env, jarray arr, Java_Type type);

#endif //MUD_LIBRARY_H
