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
    return null;
  }
//  printf("[Exception]:\n");
//  (*env)->ExceptionDescribe(env);
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

Java_Args _java_args_new(int argAmnt);

Java_Args* _java_args_new_ptr(int argAmnt);

void _java_args_add(Java_Args* args, Java_Typed_Val arg);
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

struct Java_String_Resp _java_string_new(JNIEnv *env, const char* msg);

//Java_Typed_Val _java_call_method_manual(JNIEnv* env,
//                                 jobject obj,
//                                 const char* methodName,
//                                 const char* retType,
//                                 const char* argsType);


struct Java_Exception_S {
  char* msg;
  char* stack_trace;
};

static char* _java_get_exception_msg(JNIEnv* env, jthrowable ex, jmethodID getCauseMethod, jmethodID  getStackMethod, jmethodID exToStringMethod, jmethodID frameToStringMethod,
                                     bool isTop) {

  char msg[2048];
  memset(msg, 0, 2048);

// Get the array of StackTraceElements.
  jobjectArray frames =
      (jobjectArray) (*env)->CallObjectMethod(env,
                                              ex,
          getStackMethod);
  jsize frames_length = (*env)->GetArrayLength(env, frames);

  // Add Throwable.toString() before descending
  // stack trace messages.
  size_t len = strlen(msg);
  if (0 != frames)
  {
    jstring msg_obj =
        (jstring) (*env)->CallObjectMethod(env, ex,
                                             exToStringMethod);
    const char* exMsgStr = (*env)->GetStringUTFChars(env, msg_obj, 0);
    const size_t exMsgStrLen = strlen(exMsgStr);

    // If this is not the top-of-the-trace then
    // this is a cause.
    if (isTop)
    {
      const char* causedByMsg = "\nCaused by: ";
      const size_t causedByMsgLen = strlen(causedByMsg);
      memcpy(msg + (sizeof(char) * len), causedByMsg, causedByMsgLen);
      len += causedByMsgLen;
    }
    memcpy(msg + (sizeof(char) * len), exMsgStr, exMsgStrLen);

    (*env)->ReleaseStringUTFChars(env, msg_obj, exMsgStr);
    (*env)->DeleteLocalRef(env, msg_obj);
    len += exMsgStrLen;
  }

  // Append stack trace messages if there are any.
  if (frames_length > 0)
  {
    jsize i = 0;
    for (i = 0; i < frames_length; i++)
    {
      // Get the string returned from the 'toString()'
      // method of the next frame and append it to
      // the error message.
      jobject frame = (*env)->GetObjectArrayElement(env, frames, i);
      jstring msg_obj =
          (jstring) (*env)->CallObjectMethod(env, frame, frameToStringMethod);

      const char* msg_str = (*env)->GetStringUTFChars(env, msg_obj, 0);


      const char* indentMsg = "\n    ";
      const size_t indentMsgLen = 5;
      memcpy(msg + (sizeof(char) * len), indentMsg, indentMsgLen);
      len += indentMsgLen;

      (*env)->ReleaseStringUTFChars(env, msg_obj, msg_str);
      (*env)->DeleteLocalRef(env, msg_obj);
      (*env)->DeleteLocalRef(env, frame);
    }
  }

  // If 'ex' has a cause then append the
  // stack trace messages from the cause.
  if (0 != frames)
  {
    jthrowable cause =
        (jthrowable) (*env)->CallObjectMethod(env,
            ex,
            getCauseMethod);
    if (0 != cause)
    {
      char* subFrameMsg = _java_get_exception_msg(env,
                                       cause,
                                       getCauseMethod,
                                       getStackMethod,
                                       exToStringMethod,
                                       frameToStringMethod, false);
      const size_t subFrameMsgLen = strlen(subFrameMsg);
      memcpy(msg + (sizeof(char) * len), subFrameMsg, subFrameMsgLen);
      len += subFrameMsgLen;
      free(subFrameMsg);
    }
  }
  char* finalMsg = malloc(sizeof(char) * len + 1);
  memcpy(finalMsg, msg, len);
  finalMsg[len] = '\0';
  return finalMsg;
}

void _java_add_class_path(JNIEnv* env, const char* path);
jclass mud_get_class(JNIEnv* env, const char* className);
jclass mud_get_class_of_obj(JNIEnv* env, jobject obj);

Java_Typed_Val _java_call_method_varargs(JNIEnv* env,
                                         jobject obj,
                                         const char* methodName,
                                         Java_Full_Type returnType,
                                         int argAmnt,
                                         Java_Typed_Val* args);
Java_Typed_Val _java_call_method(JNIEnv* env,
                                 jobject obj,
                                 const char* methodName,
                                 Java_Full_Type returnType,
                                 Java_Args* args);

void _java_call_method_void(JNIEnv* env,
                            jobject obj,
                            const char* methodName,
                            Java_Full_Type returnType,
                            Java_Args* args);
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

jobject _java_build_object(JNIEnv* env, jclass cls, const char* signature, const jvalue * args);

jfieldID _java_get_field_id(JNIEnv* env, const char* cls, const char* field, Java_Full_Type type);

jfieldID _java_get_field_id_by_class(JNIEnv* env, jclass cls, const char* field, Java_Full_Type type);

Java_Typed_Val _java_get_object_property(JNIEnv* env, jobject object, jfieldID field, Java_Full_Type type);

Java_Typed_Val _java_get_object_property_by_name(JNIEnv* env, jobject object, const char* field, Java_Full_Type type);


jmethodID mud_get_static_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);
jmethodID mud_get_method(JNIEnv* env, jclass cls, const char* methodName, const char* signature);



static struct JavaCallResp_S map_call_result(JNIEnv* env, Java_Type type, ptr val) {
  struct JavaCallResp_S result;
  if (type == Java_Bool) {
    result.value.b = *((jbyte*)val);
  } else if (type == Java_Int) {
    result.value.i = *((jint*)val);
  } else if (type == Java_Long) {
    result.value.j = *((jlong *)val);
  } else if (type == Java_Byte) {
    result.value.b = *((jbyte *)val);
  } else if (type == Java_Char) {
    result.value.c = *((jchar *)val);
  } else if (type == Java_Short) {
    result.value.s = *((jshort *)val);
  } else if (type == Java_Float) {
    result.value.f = *((jfloat *)val);
  } else if (type == Java_Double) {
    result.value.d = *((jdouble*)val);
  } else if (type == Java_Void) {
    result.is_void = true;
  } else if (type == Java_Object) {
    result.value.l = *((jobject*)val);
  }
  jthrowable exception = _java_jvm_check_exception(env);
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

#endif //MUD_LIBRARY_H
