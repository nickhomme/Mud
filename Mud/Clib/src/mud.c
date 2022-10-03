#include <unistd.h>
#include "../include/mud.h"

void _java_release_method_args(JNIEnv* env, const jvalue* methodArgs, Java_Args* args);
jvalue *_java_args_to_method_args(JNIEnv *env, Java_Args* args);

jthrowable _java_jvm_check_exception(JNIEnv* env) {
  if (!(*env)->ExceptionCheck(env)) {
    return null;
  }
  printf("[Exception]:\n");
  (*env)->ExceptionDescribe(env);
  jthrowable ex = (*env)->ExceptionOccurred(env);
  (*env)->ExceptionClear(env);
  return ex;
}


JavaVMOption* _java_jvm_options(size_t amnt) {
  return malloc(sizeof(JavaVMOption) * amnt);
}

JavaVMOption* _java_jvm_options_va(size_t amnt, ...) {
  JavaVMOption* options = _java_jvm_options(amnt);
  va_list argsList;
  va_start(argsList, amnt);
  for (size_t i = 0; i < amnt; i++) {
    char* arg = va_arg(argsList, char*);
    size_t len = strlen(arg);
    options[i].optionString = malloc(sizeof(char) * len + 1);
    strcpy(options[i].optionString, arg);
  }
  va_end(argsList);
  return options;
}

JavaVMOption* _java_jvm_options_str_arr(size_t amnt, const char** optionArgs) {
  JavaVMOption* options = _java_jvm_options(amnt);
  for (size_t i = 0; i < amnt; i++) {
    const char* arg = optionArgs[i];
    size_t len = strlen(arg);
    options[i].optionString = malloc(sizeof(char) * len + 1);
    strcpy(options[i].optionString, arg);
  }
  return options;
}

Java_JVM_Instance _java_jvm_create_instance(JavaVMOption* options, int optionsAmnt) {
//  printf("Creating JVMdd with args: %s\n", args);
//  const size_t argsLen = strlen(args);
//  char* argsCpy = (char*) malloc(sizeof(char) * (argsLen + 1));
//  strcpy(argsCpy, args);
  Java_JVM_Instance instance = {.env = malloc(sizeof(JNIEnv)), .jvm = malloc(sizeof(JavaVM))};
//================== prepare loading of Java VM ============================
  JavaVMInitArgs vm_args;                        // Initialization arguments
  vm_args.options = options;
  vm_args.nOptions = optionsAmnt;                          // number of options

  printf("Creating JVMdd with args:\n");
  for (int i = 0; i < optionsAmnt; ++i) {
    printf("\t%s\n", options[i].optionString);
  }


//  options[0].optionString = args;   // where to find java .cls
  vm_args.version = JNI_VERSION_1_6;             // minimum Java version

//  vm_args.ignoreUnrecognized = static_cast<jboolean>(false);     // invalid options make the JVM init fail
  vm_args.ignoreUnrecognized = false;
  //=============== load and initialize Java VM and JNI interface =============
  JavaVM** jvm = &instance.jvm;
  JNIEnv** env = &instance.env;
  JavaVMInitArgs* jvmArgs = &vm_args;
  jint rc = JNI_CreateJavaVM(jvm, (void**) env, jvmArgs);  // YES !!
//  options;    // we then no longer need the initialisation options.
  if (rc != JNI_OK) {
    // TO DO: error processing...
//    std::cin.get();
//    if (_java_jvm_check_exception(env, *env));
    exit(EXIT_FAILURE);
  }
  //=============== Display JVM version =======================================
  jint ver = (*instance.env)->GetVersion(instance.env);
  printf("JVM load succeeded: Version %i.%i\n", ((ver >> 16) & 0x0f), (ver & 0x0f));
//  std::cout << "JVM load succeeded: Version ";
//  std::cout << ((ver >> 16) & 0x0f) << "." << (ver & 0x0f) << std::endl;
  return instance;
}

void _java_add_class_path(JNIEnv* env, const char* path) {
//  char urlPath[2048];
//  sprintf(urlPath, "file://%s", path);
  printf("Adding %s to the classpath\n", path);

  jclass classLoaderCls = _java_get_class(env, "java/lang/ClassLoader");
  jclass urlClassLoaderCls = _java_get_class(env, "java/net/URLClassLoader");
  jclass urlCls = _java_get_class(env, "java/net/URL");

  jmethodID getSystemClassLoaderMethod = (*env)->GetStaticMethodID(env, classLoaderCls, "getSystemClassLoader", "()Ljava/lang/ClassLoader;");
  jobject classLoaderInstance = (*env)->CallStaticObjectMethod(env, classLoaderCls, getSystemClassLoaderMethod);
  jmethodID addUrlMethod = (*env)->GetMethodID(env, urlClassLoaderCls, "addURL", "(Ljava/net/URL;)V");
  jmethodID urlConstructor = (*env)->GetMethodID(env, urlCls, "<init>", "(Ljava/lang/String;)V");
  jstring urlPathStrObj = (*env)->NewStringUTF(env,  path);
  jobject urlInstance = (*env)->NewObject(env, urlCls, urlConstructor, urlPathStrObj);
  (*env)->CallVoidMethod(env, classLoaderInstance, addUrlMethod, urlInstance);
//  _java_string_release(env, urlPathStrObj, urlPath);
  printf("Added %s to the classpath\n", path);
}

jclass _java_get_class(JNIEnv* env, const char* className) {
  jclass cls = (*env)->FindClass(env, className);
  if (!cls) {
    printf("Error: Class %s not found\n", className);
    exit(1);
  }
//  _java_jvm_check_exception(env);
  return cls;
}

Java_Typed_Val _java_call_method_varargs(JNIEnv* env,
                                         jobject obj,
                                         const char* methodName,
                                         Java_Full_Type returnType,
                                         int argAmnt,
                                         Java_Typed_Val* argsData) {
  Java_Args args = {.args = argsData, .arg_amount = argAmnt};
  return _java_call_method(env, obj, methodName, returnType, &args);
}
Java_Typed_Val _java_call_method(JNIEnv* env,
                                 jobject obj,
                                 const char* methodName,
                                 Java_Full_Type returnType,
                                 Java_Args* args) {
  jclass cls = (*env)->GetObjectClass(env, obj);

  const char* methodTyping = _java_method_typing_string(returnType, args);
  jmethodID mid = (*env)->GetMethodID(env, cls, methodName, methodTyping);  // find method
  if (!mid) {
    printf("Method not found: %s %s\n", methodName, methodTyping);
    exit(1);
  }
  safe_free(methodTyping);
  Java_Val result;

  const jvalue* methodArgs = _java_args_to_method_args(env, args);
  if (returnType.type == Java_Bool) {
    result.bool_val = (*env)->CallBooleanMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Int) {
    result.int_val = (*env)->CallIntMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Long) {
    result.long_val = (*env)->CallLongMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Byte) {
    result.byte_val = (*env)->CallByteMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Char) {
    result.char_val = (*env)->CallCharMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Short) {
    result.short_val = (*env)->CallShortMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Float) {
    result.float_val = (*env)->CallFloatMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Double) {
    result.double_val = (*env)->CallDoubleMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Void) {
    (*env)->CallVoidMethodA(env, obj, mid, methodArgs);
  } else if (returnType.type == Java_Object) {
    result.obj_val = (*env)->CallObjectMethodA(env, obj, mid, methodArgs);
  }
  jthrowable exception = _java_jvm_check_exception(env);
  if (exception) {
    result.obj_val = exception;
    returnType.object_type = Java_Object_Throwable;
  } else if (returnType.object_type == Java_Object_String) {
    jstring resultStr = ((jstring) result.obj_val);
    result.string_val.jstring = resultStr;
    result.string_val.char_ptr = _java_jstring_to_string(env, resultStr);
  }
  _java_release_method_args(env, methodArgs, args);

  Java_Typed_Val typedResult = {
      .type = {
          .type = returnType.type,
          .object_type = returnType.object_type,
      },
      .val = result
  };
  return typedResult;
}
Java_Typed_Val _java_call_static_method_varargs(JNIEnv* env,
                                                jclass cls,
                                                const char* methodName,
                                                Java_Full_Type returnType,
                                                int argAmnt,
                                                Java_Typed_Val* argsData) {
  Java_Args args = {.args = argsData, .arg_amount = argAmnt};
  return _java_call_static_method(env, cls, methodName, returnType, &args);
}
Java_Typed_Val _java_call_static_method(JNIEnv* env,
                                        jclass cls,
                                        const char* methodName,
                                        Java_Full_Type returnType,
                                        Java_Args* args) {
  const char* methodTyping = _java_method_typing_string(returnType, args);
  jmethodID mid = (*env)->GetStaticMethodID(env, cls, methodName, methodTyping);  // find method
  if (!mid) {
    printf("ERROR: method `%s`::`%s` not found !\n", methodName, methodTyping);
    exit(1);
  }
  safe_free(methodTyping);

//  (*env)->CallStaticMethod(env, cls, mid);                      // call method
//  jobject result = (*env)->CallStaticObjectMethodA(env, cls, mid, methodArgs);
//  _java_jvm_check_exception(env);
//
//  Java_Full_Type  value = {.type_data = returnType, .value = result};
//  return value;

  Java_Val result;
  const jvalue* methodArgs = _java_args_to_method_args(env, args);
  if (returnType.type == Java_Bool) {
    result.bool_val = (*env)->CallStaticBooleanMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Int) {
    result.int_val = (*env)->CallStaticIntMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Long) {
    result.long_val = (*env)->CallStaticLongMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Byte) {
    result.byte_val = (*env)->CallStaticByteMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Char) {
    result.char_val = (*env)->CallStaticCharMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Short) {
    result.short_val = (*env)->CallStaticShortMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Float) {
    result.float_val = (*env)->CallStaticFloatMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Double) {
    result.double_val = (*env)->CallStaticDoubleMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Void) {
    (*env)->CallStaticVoidMethodA(env, cls, mid, methodArgs);
  } else if (returnType.type == Java_Object) {
    result.obj_val = (*env)->CallStaticObjectMethodA(env, cls, mid, methodArgs);
  }

  jthrowable exception = _java_jvm_check_exception(env);
  if (exception) {
    result.obj_val = exception;
    returnType.object_type = Java_Object_Throwable;
  } else if (returnType.object_type == Java_Object_String) {
    jstring resultStr = ((jstring) result.obj_val);
    result.string_val.jstring = resultStr;
    result.string_val.char_ptr = _java_jstring_to_string(env, resultStr);
  }
  _java_release_method_args(env, methodArgs, args);


  int i = 1;

//  while (i) {
//    usleep(1 * 1000 * 1000);
//  }
  Java_Typed_Val typedResult = {
      .type = {
          .type = returnType.type,
          .object_type = returnType.object_type,
        },
      .val = result
  };
  return typedResult;
//  return (Java_Typed_Val) {.val = result };

}

void _java_release_object(JNIEnv* env, jobject obj) {
  (*env)->DeleteLocalRef(env, obj);
}
jobject _java_build_object(JNIEnv* env, jclass cls, Java_Args* args) {
  char* argsStr = _java_args_to_args_type(args);

  size_t argsStrLen = strlen(argsStr);
  argsStr = realloc(argsStr, argsStrLen + 2);
  argsStr[argsStrLen] = 'V';
  argsStr[argsStrLen + 1] = '\0';
  printf("Looking for ctor with args: %s\n", argsStr);
  jmethodID ctor = (*env)->GetMethodID(env, cls, "<init>", argsStr);  // FIND AN OBJECT CONSTRUCTOR
  if (!ctor) {
    printf("ERROR: constructor not found matching: %s !\n", argsStr);
    free(argsStr);
    return NULL;
  }
  free(argsStr);
//  printf("Found ctor: %p\n", ctor);
  if (!args) {
    return (*env)->NewObject(env, cls, ctor);
  }
  const jvalue* ctorArgs = _java_args_to_method_args(env, args);
  jobject obj = (*env)->NewObjectA(env, cls, ctor, ctorArgs);
  _java_release_method_args(env, ctorArgs, args);
  return obj;
}

void _java_jvm_destroy_instance(JavaVM* jvm) {
  (*jvm)->DestroyJavaVM(jvm);
  printf("JVM destroyed\n");
}
Java_Args _java_args_new(int argAmnt) {
  Java_Args args = {.args = (Java_Typed_Val*) malloc(sizeof(Java_Typed_Val) * argAmnt), .arg_amount =  argAmnt};
  return args;
}
Java_Args* _java_args_new_ptr(int argAmnt) {
  Java_Args args = _java_args_new(argAmnt);
  Java_Args* argsPtr = (Java_Args*) malloc(sizeof(Java_Args));
  memcpy(argsPtr, &args, sizeof(Java_Args));
  return argsPtr;
}
jclass _java_get_obj_class(JNIEnv* env, jobject obj) {
  return (*env)->GetObjectClass(env, obj);
}
Java_Typed_Val _java_call_static_method_named(JNIEnv* env,
                                              const char* className,
                                              const char* methodName,
                                              Java_Full_Type returnType,
                                              Java_Args* args) {
  jclass
      cls = _java_get_class(env, className);
  return _java_call_static_method(env,
                                  cls, methodName, returnType, args);
}
Java_Typed_Val _java_call_static_method_named_varargs(JNIEnv* env,
                                                      const char* className,
                                                      const char* methodName,
                                                      Java_Full_Type returnType,
                                                      int argAmnt,
                                                      Java_Typed_Val* args) {
  jclass
      cls = _java_get_class(env, className);
  return _java_call_static_method_varargs(env,
                                          cls, (methodName), (returnType), argAmnt, args);
}
jobject _java_build_class_object(JNIEnv* env, const char* className, Java_Args* args) {
  jclass
      cls = _java_get_class(env, className);
  return _java_build_object(env, cls, args);
//  return NULL;
}
jfieldID _java_get_field_id(JNIEnv* env, const char* cls, const char* field, Java_Full_Type type) {
  jclass javaClass = _java_get_class(env,
                                     cls);
  const char* typeStr = _java_get_obj_type_string(type);
  jfieldID jfieldId = (*env)->GetFieldID(env, javaClass, field, typeStr);

  if (type.object_type == Java_Object_Custom) {
    safe_free(typeStr);
  }
  return jfieldId;
}
jfieldID _java_get_field_id_by_class(JNIEnv* env, jclass cls, const char* field, Java_Full_Type type) {
  const char* typeStr = _java_get_obj_type_string(type);
  jfieldID jfieldId = (*env)->GetFieldID(env, cls, field, typeStr);
  if (type.object_type == Java_Object_Custom) {
    safe_free(typeStr);
  }
  return jfieldId;
}
Java_Typed_Val _java_get_object_property(JNIEnv* env, jobject object, jfieldID field, Java_Full_Type type) {
  Java_Val result;
  switch (type.type) {
    case Java_Int: {
      result.int_val = (*env)->GetIntField(env, object, field);
      break;
    }
    case Java_Bool: {
      result.bool_val = (*env)->GetBooleanField(env, object, field);
      break;
    }
    case Java_Byte: {
      result.byte_val = (*env)->GetByteField(env, object, field);
      break;
    }
    case Java_Char: {
      result.char_val = (*env)->GetCharField(env, object, field);
      break;
    }
    case Java_Short: {
      result.short_val = (*env)->GetShortField(env, object, field);
      break;
    }
    case Java_Long: {
      result.long_val = (*env)->GetLongField(env, object, field);
      break;
    }
    case Java_Float: {
      result.float_val = (*env)->GetFloatField(env, object, field);
      break;
    }
    case Java_Double: {
      result.double_val = (*env)->GetDoubleField(env, object, field);
      break;
    }
    case Java_Object: {
      result.obj_val = (*env)->GetObjectField(env, object, field);
      break;
    }
    case Java_Void:
      break;
  }

  if (type.object_type == Java_Object_String) {
    jstring resultStr = ((jstring) result.obj_val);
    result.string_val.jstring = resultStr;
    result.string_val.char_ptr = _java_jstring_to_string(env, resultStr);
  }

  return (Java_Typed_Val) {
      .type = type,
      .val = result
  };
}
Java_Typed_Val _java_get_object_property_by_name(JNIEnv* env, jobject object, const char* field, Java_Full_Type type) {
  jclass cls = _java_get_obj_class(env, object);
  const char* typeStr = _java_get_obj_type_string(type);
  jfieldID fieldId = (*env)->GetFieldID(env, cls, field, typeStr);
  if (type.object_type == Java_Object_Custom) {
    safe_free(typeStr);
  }
  return _java_get_object_property(env, object, fieldId, type);
}
void _java_args_add(Java_Args* args, Java_Typed_Val arg) {
  args->args[args->current_arg] = arg;
  args->current_arg++;
}
void interop_free(ptr pointer) {
  safe_free(pointer);
}
void _java_string_release(JNIEnv* env, jstring message, const char* msgChars) {
  (*env)->ReleaseStringUTFChars(env, message, msgChars);
//  _java_release_object(env, message);
}

jvalue *_java_args_to_method_args(JNIEnv *env, Java_Args* args) {
  jvalue *methodArgs = _java_args_to_method_args_new(args);
  for (size_t i = 0; i < (*args).arg_amount; i++) {
    methodArgs[i] = _java_args_to_method_arg_to_jvalue(env, (*args).args[i]);
  }
  return methodArgs;
}


void _java_release_method_args(JNIEnv* env, const jvalue* methodArgs, Java_Args* args) {
  for (size_t i = 0; i < (*args).arg_amount; i++) {
    jvalue jArg = methodArgs[i];
    Java_Typed_Val arg = (*args).args[i];
    if (arg.type.object_type == Java_Object_String) {
      _java_release_object(env, jArg.l);
    }

  }
}

//Java_Typed_Val _java_call_method_manual(JNIEnv* env,
//                                        jobject obj,
//                                        jclass class,
//                                        const char* methodName,
//                                        const char* methodTyping) {
//  jmethodID method = (*env)->GetMethodID(env, class, methodName, types);
//  jclass urlCls = env->FindClass("java/net/URL");
//  jmethodID urlConstructor = env->GetMethodID(urlCls, "<init>", "(Ljava/lang/String;)V");
//  jobject urlInstance = env->NewObject(urlCls, urlConstructor, env->NewStringUTF(urlPath.c_str()));
//  env->CallVoidMethod(classLoaderInstance, addUrlMethod, urlInstance);
//  std::cout << "Added " << urlPath << " to the classpath." << std::endl;
//}