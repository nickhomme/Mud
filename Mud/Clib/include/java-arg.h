//
// Created by Nicholas Homme on 1/16/20.
//

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wswitch"
#ifndef CT400_SRC_JVM_JAVA_ARG_H_
#define CT400_SRC_JVM_JAVA_ARG_H_

#include <jni.h>
#include <signal.h>
#include "../src/memory-util.h"

typedef enum _Java_Object_Type {
  Java_Object_String = 1,
  Java_Object_Class,
  Java_Object_Throwable,
  Java_Object_Array,
  Java_Object_Custom
} Java_Object_Type;

typedef enum _Java_Type {
  Java_Int = 1,
  Java_Bool,
  Java_Byte,
  Java_Char,
  Java_Short,
  Java_Long,
  Java_Float,
  Java_Double,
  Java_Object,
  Java_Void,
} Java_Type;

typedef struct _Java_Val {
  unsigned char bool_val;
  unsigned char byte_val;
  char char_val;
  int int_val;
  short short_val;
  long long_val;
  float float_val;
  double double_val;
  struct {
    ptr jstring;
    const char* char_ptr;
  } string_val;
  jobject obj_val;
  jobjectArray obj_arr_val;
} Java_Val;


typedef struct _Java_Full_Type {
  Java_Type type;
  Java_Object_Type object_type;
//  void *value;
  const char* custom_type;
} Java_Full_Type;


typedef struct _Java_Typed_Val {
  Java_Full_Type type;
  Java_Val val;
} Java_Typed_Val;

typedef struct _Java_Args {
  Java_Typed_Val *args;
  u_int16_t arg_amount;
  u_int16_t current_arg;
} Java_Args;

//typedef struct _Java_Full_Type {
//  Java_Full_Type type_data;
//  ptr value;
//} Java_Value;

//static char* _java_arg_value_to_string(Java_Value) {
//
//}
static const char * _java_type_to_string(Java_Type type, bool capitalize) {
  switch (type) {
    case Java_Int: return capitalize ? "Int" : "int";
    case Java_Bool: return capitalize ? "Boolean" : "boolean";
    case Java_Byte: return capitalize ? "Byte" : "byte";
    case Java_Char: return capitalize ? "Char" : "char";
    case Java_Short: return capitalize ? "Short" : "short";
    case Java_Long: return capitalize ? "Long" : "long";
    case Java_Float: return capitalize ? "Float" : "float";
    case Java_Double: return capitalize ? "Double" : "double";
    case Java_Object: return capitalize ? "Object" : "object";
    case Java_Void: return capitalize ? "Void" : "void";
  }
  return "";
}

static Java_Full_Type _java_type_object_custom(const char* customType) {
  Java_Full_Type type = {.type = Java_Object, .object_type = Java_Object_Custom, .custom_type = customType};
  return type;
}
static Java_Full_Type _java_type_object(Java_Object_Type objType) {
  Java_Full_Type type = {.type = Java_Object, .object_type = objType};
  return type;
}
static Java_Full_Type _java_type(Java_Type dataType) {
  Java_Full_Type type = {.type = dataType};
  return type;
}

static jstring _java_string_new(JNIEnv *env, const char* msg) {
  return (*env)->NewStringUTF(env, msg);
}

static void *_java_arg_value_ptr(uint64_t size, const void *value) {
  void *ptr = malloc(size);
  memcpy(ptr, value, size);
  return ptr;
}

static Java_Typed_Val _java_arg_new_string(const char *val) {
  Java_Full_Type arg = {
      .type = Java_Object,
      .object_type = Java_Object_String,
  };
//  printf("[Arg_String]: %s -> %s\n", val, arg.value);
  return (Java_Typed_Val) {
    .type = arg,
      .val = {
          .string_val = {
              .char_ptr = val
          }
      }
  };
}

static Java_Typed_Val _java_arg_new_int(int val) {
  Java_Full_Type arg = {
      .type = Java_Int,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .int_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_float(float val) {
  Java_Full_Type arg = {
      .type = Java_Float,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .float_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_decimal(double val) {
  Java_Full_Type arg = {
      .type = Java_Double,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .double_val = val
      }
  };
}
static Java_Typed_Val _java_arg_new_bool(bool val) {
  Java_Full_Type arg = {
      .type = Java_Bool,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .bool_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_byte(unsigned char val) {
  Java_Full_Type arg = {
      .type = Java_Byte,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .byte_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_char(char val) {
  Java_Full_Type arg = {
      .type = Java_Char,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .char_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_short(short val) {
  Java_Full_Type arg = {
      .type = Java_Short,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .short_val = val
      }
  };
}

static Java_Typed_Val _java_arg_new_long(long val) {
  Java_Full_Type arg = {
      .type = Java_Long,
  };
  return (Java_Typed_Val) {
      .type = arg,
      .val = {
          .long_val = val
      }
  };
}

//static Java_Args _java_args_new_with_args(int argAmnt, ...) {
//  Java_Args args = {.args = (Java_Full_Type*) malloc(sizeof(Java_Full_Type)*argAmnt), .arg_amount = static_cast<u_int16_t>(argAmnt)};
//
//  va_list argsList;
//  va_start(argsList, argAmnt);
//
//  for (size_t i = 0; i < argAmnt; i++) {
//    Java_Full_Type arg = va_arg(argsList, Java_Full_Type);
//    _java_args_add(&args, arg);
//  }
//
//  va_end(argsList);
//  return args;
//}

static void _java_args_destroy(Java_Args *args) {
//  safe_free(args->args);
}

static jvalue *_java_args_to_method_args_new(Java_Args* args) {
  return (jvalue*) malloc(sizeof(jvalue *)*(*args).arg_amount);
}

static jvalue _java_args_to_method_arg_to_jvalue(JNIEnv *env, Java_Typed_Val arg) {
  jvalue val = {};

  switch (arg.type.type) {
    case Java_Int: {
      val.i = arg.val.int_val;
      break;
    }
    case Java_Bool: {
      val.z = arg.val.bool_val;
      break;
    }
    case Java_Byte: {
      val.b = arg.val.byte_val;
      break;
    }
    case Java_Char: {
      val.c = arg.val.char_val;
      break;
    }
    case Java_Short: {
      val.s = arg.val.short_val;
      break;
    }
    case Java_Long: {
      val.j = arg.val.long_val;
      break;
    }
    case Java_Float: {
      val.f = arg.val.float_val;
      break;
    }
    case Java_Double: {
      val.d = arg.val.double_val;
      break;
    }
  }
  if (arg.type.type == Java_Object) {
    switch (arg.type.object_type) {
      case Java_Object_String: {
        val.l = _java_string_new(env, arg.val.string_val.char_ptr);
        break;
      }
      case Java_Object_Class: {
        val.l = arg.val.obj_val;
        break;
      }
      case Java_Object_Throwable: {
        val.l = arg.val.obj_val;
      }
      case Java_Object_Array: {
        val.l = arg.val.obj_arr_val;
      }
      case Java_Object_Custom:break;
    }
  }
  return val;
}

static const char * _java_get_obj_type_string(Java_Full_Type typeData) {
  char* str;
  switch (typeData.type) {
    case Java_Int: str = "I";
      break;
    case Java_Bool: str = "Z";
      break;
    case Java_Byte: str = "B";
      break;
    case Java_Char: str = "C";
      break;
    case Java_Short: str = "S";
      break;
    case Java_Long: str = "J";
      break;
    case Java_Float: str = "F";
      break;
    case Java_Double: str = "D";
      break;
    case Java_Void: str = "V";
      break;
    case Java_Object: {
      str = "L";
      switch (typeData.object_type) {
        case Java_Object_String: {
          str = "Ljava/lang/String";
          break;
        }
        case Java_Object_Custom: {
          unsigned long customLen = strlen(typeData.custom_type);
          str = malloc(sizeof(char) * 2 + customLen);
          memset(str, '\0', sizeof(char) * 2 + customLen);
          str[0] = 'L';
          memcpy(str + 1, typeData.custom_type, customLen);
          char *ix = str;
          int n = 0;
          while((ix = strchr(ix, '.')) != NULL) {
            *ix++ = '/';
            n++;
          }
//          str[customLen + 1] = '\0';
          return str;
        }
      }
    }
  }
//  string_append_chars(&str, ";");
  return str;
}

static char * _java_args_to_args_type(Java_Args* args) {
  if (!args) {
    char* str = malloc(sizeof(char) * 3);
    str[0] = '(';
    str[1] = ')';
    str[2] = '\0';
    return str;
  }

  char* str = malloc(sizeof(char) + 2048);
  memset(str, '\0', sizeof(char) + 2048);
  str[0] = '(';

  uint32_t offset = 1;
  for (size_t i = 0; i < (*args).arg_amount; i++) {

    Java_Typed_Val arg = (*args).args[i];
    char* type = (char*) _java_get_obj_type_string(arg.type);

    const unsigned long typeLen = strlen(type);
    memcpy(str + offset, type, typeLen);
    offset+=typeLen;
    if (arg.type.type == Java_Object) {
      str[offset] = ';';
      offset++;

      if(arg.type.object_type == Java_Object_Custom) {
        safe_free(type);
      }
    }

  }
//  offset++;
  str[offset] = ')';

  char* finalStr = malloc(sizeof(char) * offset + 2);
  memcpy(finalStr, str, offset + 2);
  finalStr[offset + 2] = '\0';
  safe_free(str);
  return finalStr;
}
static const char* _java_method_typing_string(Java_Full_Type returnType, Java_Args* args) {
  const char* str = _java_args_to_args_type(args);
  const char* returnTypeStr = _java_get_obj_type_string(returnType);
  const unsigned long argsLen = strlen(str);
  const unsigned long returnLen = strlen(returnTypeStr);

  const int semiColon = returnType.type == Java_Object;

  char* merged = malloc(sizeof(char) * (argsLen + returnLen + 1 + semiColon));

  memcpy(merged, str, argsLen);
  memcpy(merged + argsLen, returnTypeStr, returnLen);
  if (semiColon) {
    merged[argsLen + returnLen] = ';';
  }
  merged[argsLen + returnLen + 1] = '\0';
  return merged;
}


#endif //CT400_SRC_JVM_JAVA_ARG_H_

#pragma clang diagnostic pop